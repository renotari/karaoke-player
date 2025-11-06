using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Manages application cache with LRU eviction policy
/// </summary>
public class CacheManager : ICacheManager, IDisposable
{
    private readonly string _cacheDirectory;
    private readonly string _cacheIndexPath;
    private readonly long _maxThumbnailCacheSizeBytes;
    private readonly Dictionary<CacheCategory, Dictionary<string, CacheEntry>> _cache;
    private readonly LinkedList<CacheKey> _lruList;
    private readonly Dictionary<string, LinkedListNode<CacheKey>> _lruNodes;
    private long _currentThumbnailSizeBytes;
    private long _hits;
    private long _misses;
    private IMediaLibraryManager? _mediaLibraryManager;

    public CacheManager(string cacheDirectory, long maxThumbnailCacheSizeMB = 500)
    {
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        _cacheIndexPath = Path.Combine(_cacheDirectory, "cache-index.json");
        _maxThumbnailCacheSizeBytes = maxThumbnailCacheSizeMB * 1024 * 1024;
        
        _cache = new Dictionary<CacheCategory, Dictionary<string, CacheEntry>>
        {
            { CacheCategory.Thumbnail, new Dictionary<string, CacheEntry>() },
            { CacheCategory.Metadata, new Dictionary<string, CacheEntry>() },
            { CacheCategory.Search, new Dictionary<string, CacheEntry>() }
        };
        
        _lruList = new LinkedList<CacheKey>();
        _lruNodes = new Dictionary<string, LinkedListNode<CacheKey>>();
        _currentThumbnailSizeBytes = 0;

        // Ensure cache directory exists
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Subscribes to media library manager events for cache invalidation
    /// </summary>
    public void SubscribeToMediaLibraryEvents(IMediaLibraryManager mediaLibraryManager)
    {
        if (_mediaLibraryManager != null)
        {
            // Unsubscribe from previous manager
            _mediaLibraryManager.FilesAdded -= OnFilesChanged;
            _mediaLibraryManager.FilesRemoved -= OnFilesChanged;
            _mediaLibraryManager.FilesModified -= OnFilesChanged;
        }

        _mediaLibraryManager = mediaLibraryManager ?? throw new ArgumentNullException(nameof(mediaLibraryManager));
        
        // Subscribe to file change events
        _mediaLibraryManager.FilesAdded += OnFilesChanged;
        _mediaLibraryManager.FilesRemoved += OnFilesChanged;
        _mediaLibraryManager.FilesModified += OnFilesChanged;
    }

    /// <summary>
    /// Sets a value in the cache
    /// </summary>
    public void Set(string key, object value, CacheCategory category)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var entry = new CacheEntry
        {
            Key = key,
            Value = value,
            Category = category,
            Timestamp = DateTime.UtcNow,
            SizeBytes = EstimateSize(value)
        };

        lock (_cache)
        {
            var categoryCache = _cache[category];

            // Remove existing entry if present
            if (categoryCache.ContainsKey(key))
            {
                RemoveFromLRU(key, category);
                var oldEntry = categoryCache[key];
                if (category == CacheCategory.Thumbnail)
                {
                    _currentThumbnailSizeBytes -= oldEntry.SizeBytes;
                }
            }

            // Add new entry
            categoryCache[key] = entry;
            AddToLRU(key, category);

            // Update size tracking for thumbnails
            if (category == CacheCategory.Thumbnail)
            {
                _currentThumbnailSizeBytes += entry.SizeBytes;
                
                // Evict if over size limit
                while (_currentThumbnailSizeBytes > _maxThumbnailCacheSizeBytes && _lruList.Count > 0)
                {
                    EvictLRU();
                }
            }
        }
    }

    /// <summary>
    /// Gets a value from the cache
    /// </summary>
    public object? Get(string key, CacheCategory category)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        lock (_cache)
        {
            var categoryCache = _cache[category];

            if (categoryCache.TryGetValue(key, out var entry))
            {
                _hits++;
                
                // Update LRU
                RemoveFromLRU(key, category);
                AddToLRU(key, category);
                
                return entry.Value;
            }

            _misses++;
            return null;
        }
    }

    /// <summary>
    /// Gets a typed value from the cache
    /// </summary>
    public T? Get<T>(string key, CacheCategory category) where T : class
    {
        var value = Get(key, category);
        return value as T;
    }

    /// <summary>
    /// Invalidates a specific cache entry
    /// </summary>
    public void Invalidate(string key, CacheCategory category)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        lock (_cache)
        {
            var categoryCache = _cache[category];

            if (categoryCache.TryGetValue(key, out var entry))
            {
                categoryCache.Remove(key);
                RemoveFromLRU(key, category);

                if (category == CacheCategory.Thumbnail)
                {
                    _currentThumbnailSizeBytes -= entry.SizeBytes;
                }
            }
        }
    }

    /// <summary>
    /// Invalidates all entries in a category
    /// </summary>
    public void InvalidateAll(CacheCategory category)
    {
        lock (_cache)
        {
            var categoryCache = _cache[category];
            var keys = categoryCache.Keys.ToList();

            foreach (var key in keys)
            {
                var entry = categoryCache[key];
                RemoveFromLRU(key, category);

                if (category == CacheCategory.Thumbnail)
                {
                    _currentThumbnailSizeBytes -= entry.SizeBytes;
                }
            }

            categoryCache.Clear();
        }
    }

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    public void Clear()
    {
        lock (_cache)
        {
            foreach (var category in _cache.Keys)
            {
                _cache[category].Clear();
            }

            _lruList.Clear();
            _lruNodes.Clear();
            _currentThumbnailSizeBytes = 0;
            _hits = 0;
            _misses = 0;
        }
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public CacheStats GetCacheStats()
    {
        lock (_cache)
        {
            var stats = new CacheStats
            {
                Hits = _hits,
                Misses = _misses,
                ThumbnailCount = _cache[CacheCategory.Thumbnail].Count,
                ThumbnailSizeBytes = _currentThumbnailSizeBytes,
                MetadataCount = _cache[CacheCategory.Metadata].Count,
                SearchCount = _cache[CacheCategory.Search].Count
            };

            // Calculate sizes for metadata and search
            stats.MetadataSizeBytes = _cache[CacheCategory.Metadata].Values.Sum(e => e.SizeBytes);
            stats.SearchSizeBytes = _cache[CacheCategory.Search].Values.Sum(e => e.SizeBytes);
            
            stats.TotalSizeBytes = stats.ThumbnailSizeBytes + stats.MetadataSizeBytes + stats.SearchSizeBytes;
            stats.TotalEntries = stats.ThumbnailCount + stats.MetadataCount + stats.SearchCount;

            return stats;
        }
    }

    /// <summary>
    /// Persists the cache index to disk
    /// </summary>
    public async Task SaveCacheIndexAsync()
    {
        try
        {
            CacheIndex index;
            
            lock (_cache)
            {
                index = new CacheIndex
                {
                    Version = 1,
                    Timestamp = DateTime.UtcNow,
                    Entries = new List<CacheIndexEntry>()
                };

                foreach (var category in _cache.Keys)
                {
                    foreach (var entry in _cache[category].Values)
                    {
                        index.Entries.Add(new CacheIndexEntry
                        {
                            Key = entry.Key,
                            Category = entry.Category.ToString(),
                            Timestamp = entry.Timestamp,
                            SizeBytes = entry.SizeBytes
                        });
                    }
                }
            }

            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_cacheIndexPath, json);
        }
        catch (Exception)
        {
            // Log error but don't crash
        }
    }

    /// <summary>
    /// Loads the cache index from disk
    /// </summary>
    public async Task LoadCacheIndexAsync()
    {
        try
        {
            if (!File.Exists(_cacheIndexPath))
                return;

            var json = await File.ReadAllTextAsync(_cacheIndexPath);
            var index = JsonSerializer.Deserialize<CacheIndex>(json);

            if (index == null || index.Version != 1)
                return;

            // Note: We only load the index metadata, not the actual cached values
            // Actual values will be loaded on-demand or regenerated
            // This is intentional to keep startup fast
        }
        catch (Exception)
        {
            // Log error but don't crash
        }
    }

    private void OnFilesChanged(object? sender, MediaFilesChangedEventArgs e)
    {
        if (e.Files == null || e.Files.Count == 0)
            return;

        // Invalidate cache for changed files
        foreach (var file in e.Files)
        {
            Invalidate(file.Id, CacheCategory.Thumbnail);
            Invalidate(file.Id, CacheCategory.Metadata);
        }

        // Invalidate search cache when files change
        InvalidateAll(CacheCategory.Search);
    }

    private void AddToLRU(string key, CacheCategory category)
    {
        var cacheKey = new CacheKey { Key = key, Category = category };
        var node = _lruList.AddFirst(cacheKey);
        _lruNodes[$"{category}:{key}"] = node;
    }

    private void RemoveFromLRU(string key, CacheCategory category)
    {
        var nodeKey = $"{category}:{key}";
        if (_lruNodes.TryGetValue(nodeKey, out var node))
        {
            _lruList.Remove(node);
            _lruNodes.Remove(nodeKey);
        }
    }

    private void EvictLRU()
    {
        if (_lruList.Last == null)
            return;

        var lruKey = _lruList.Last.Value;
        var categoryCache = _cache[lruKey.Category];

        if (categoryCache.TryGetValue(lruKey.Key, out var entry))
        {
            categoryCache.Remove(lruKey.Key);

            if (lruKey.Category == CacheCategory.Thumbnail)
            {
                _currentThumbnailSizeBytes -= entry.SizeBytes;
            }
        }

        _lruList.RemoveLast();
        _lruNodes.Remove($"{lruKey.Category}:{lruKey.Key}");
    }

    private long EstimateSize(object value)
    {
        // Rough estimation of object size in bytes
        if (value is string str)
        {
            return str.Length * 2; // Unicode characters are 2 bytes
        }
        else if (value is byte[] bytes)
        {
            return bytes.Length;
        }
        else if (value is MediaMetadata)
        {
            return 1024; // Approximate size for metadata object
        }
        else
        {
            // Default estimate for other objects
            return 512;
        }
    }

    public void Dispose()
    {
        if (_mediaLibraryManager != null)
        {
            _mediaLibraryManager.FilesAdded -= OnFilesChanged;
            _mediaLibraryManager.FilesRemoved -= OnFilesChanged;
            _mediaLibraryManager.FilesModified -= OnFilesChanged;
        }

        GC.SuppressFinalize(this);
    }

    private class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public object Value { get; set; } = null!;
        public CacheCategory Category { get; set; }
        public DateTime Timestamp { get; set; }
        public long SizeBytes { get; set; }
    }

    private class CacheKey
    {
        public string Key { get; set; } = string.Empty;
        public CacheCategory Category { get; set; }
    }

    private class CacheIndex
    {
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
        public List<CacheIndexEntry> Entries { get; set; } = new();
    }

    private class CacheIndexEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long SizeBytes { get; set; }
    }
}
