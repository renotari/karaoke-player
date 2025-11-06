using System;
using System.Threading.Tasks;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for managing application cache
/// </summary>
public interface ICacheManager
{
    /// <summary>
    /// Sets a value in the cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="category">Cache category (thumbnail, metadata, search)</param>
    void Set(string key, object value, CacheCategory category);

    /// <summary>
    /// Gets a value from the cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="category">Cache category</param>
    /// <returns>Cached value or null if not found</returns>
    object? Get(string key, CacheCategory category);

    /// <summary>
    /// Gets a typed value from the cache
    /// </summary>
    /// <typeparam name="T">Type of value to retrieve</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="category">Cache category</param>
    /// <returns>Cached value or default if not found</returns>
    T? Get<T>(string key, CacheCategory category) where T : class;

    /// <summary>
    /// Invalidates a specific cache entry
    /// </summary>
    /// <param name="key">Cache key to invalidate</param>
    /// <param name="category">Cache category</param>
    void Invalidate(string key, CacheCategory category);

    /// <summary>
    /// Invalidates all entries in a category
    /// </summary>
    /// <param name="category">Cache category to clear</param>
    void InvalidateAll(CacheCategory category);

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    /// <returns>Cache statistics</returns>
    CacheStats GetCacheStats();

    /// <summary>
    /// Persists the cache index to disk
    /// </summary>
    Task SaveCacheIndexAsync();

    /// <summary>
    /// Loads the cache index from disk
    /// </summary>
    Task LoadCacheIndexAsync();
}

/// <summary>
/// Cache category enumeration
/// </summary>
public enum CacheCategory
{
    Thumbnail,
    Metadata,
    Search
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStats
{
    public long TotalSizeBytes { get; set; }
    public int TotalEntries { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }
    public double HitRate => (Hits + Misses) > 0 ? (double)Hits / (Hits + Misses) * 100 : 0;
    public int ThumbnailCount { get; set; }
    public long ThumbnailSizeBytes { get; set; }
    public int MetadataCount { get; set; }
    public long MetadataSizeBytes { get; set; }
    public int SearchCount { get; set; }
    public long SearchSizeBytes { get; set; }
}
