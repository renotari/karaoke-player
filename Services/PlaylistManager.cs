using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using Microsoft.EntityFrameworkCore;

namespace KaraokePlayer.Services;

/// <summary>
/// Manages the playlist queue with auto-save and M3U support
/// </summary>
public class PlaylistManager : IPlaylistManager
{
    private readonly IDbContextFactory _dbContextFactory;
    private readonly string _autoSaveFilePath;
    private readonly ObservableCollection<PlaylistItem> _currentPlaylist;
    private Timer? _autoSaveTimer;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    // Auto-save: the playlist is snapshotted when it changes (on the thread that
    // changes it, so the timer never reads a list being modified); the timer writes
    // the latest snapshot. Revisions make sure a change made during a write is
    // written too.
    private readonly object _snapshotLock = new();
    private List<SavedPlaylistItem> _snapshot = new();
    private long _revision;
    private long _savedRevision;

    public ObservableCollection<PlaylistItem> CurrentPlaylist => _currentPlaylist;

    /// <summary>Entries of the last loaded M3U that are not in the library.</summary>
    public int LastLoadMissingCount { get; private set; }

    public event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;

    public PlaylistManager(IDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        
        // Set up auto-save file path in user data directory
        _autoSaveFilePath = AppPaths.CurrentPlaylistFile;

        _currentPlaylist = new ObservableCollection<PlaylistItem>();
        _currentPlaylist.CollectionChanged += (s, e) =>
        {
            TakeSnapshot();
            ScheduleAutoSave();
        };
    }

    public async Task AddSongAsync(MediaFile mediaFile, string position = "next")
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        if (position != "next" && position != "end")
            throw new ArgumentException("Position must be 'next' or 'end'", nameof(position));

        // "next" = right after the song at position 0 (the one playing)
        var insertIndex = position == "next"
            ? (_currentPlaylist.Count > 0 ? 1 : 0)
            : _currentPlaylist.Count;

        await InsertSongAsync(mediaFile, insertIndex);
    }

    public async Task InsertSongAsync(MediaFile mediaFile, int index)
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        var insertIndex = Math.Clamp(index, 0, _currentPlaylist.Count);

        // Check if it's a duplicate
        var isDuplicate = IsDuplicate(mediaFile);

        // Create playlist item
        var playlistItem = new PlaylistItem
        {
            MediaFileId = mediaFile.Id,
            MediaFile = mediaFile,
            Position = insertIndex,
            AddedAt = DateTime.UtcNow,
            IsDuplicate = isDuplicate
        };

        // Insert at the specified position
        _currentPlaylist.Insert(insertIndex, playlistItem);

        // Update positions for all items after the insertion
        await UpdatePositionsAsync();

        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
        {
            ChangeType = PlaylistChangeType.Added,
            Index = insertIndex,
            Item = playlistItem
        });
    }

    public async Task RemoveSongAsync(int index)
    {
        if (index < 0 || index >= _currentPlaylist.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var item = _currentPlaylist[index];
        _currentPlaylist.RemoveAt(index);

        // Update positions for all items after removal
        await UpdatePositionsAsync();

        // Update duplicate flags
        UpdateDuplicateFlags();

        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
        {
            ChangeType = PlaylistChangeType.Removed,
            Index = index,
            Item = item
        });
    }

    public async Task ReorderSongAsync(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _currentPlaylist.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));

        if (toIndex < 0 || toIndex >= _currentPlaylist.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));

        if (fromIndex == toIndex)
            return;

        var item = _currentPlaylist[fromIndex];
        _currentPlaylist.RemoveAt(fromIndex);
        _currentPlaylist.Insert(toIndex, item);

        // Update positions
        await UpdatePositionsAsync();

        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
        {
            ChangeType = PlaylistChangeType.Reordered,
            Index = toIndex,
            Item = item
        });
    }

    public async Task ClearPlaylistAsync(bool keepFirst = false)
    {
        if (keepFirst && _currentPlaylist.Count > 0)
        {
            while (_currentPlaylist.Count > 1)
                _currentPlaylist.RemoveAt(_currentPlaylist.Count - 1);
        }
        else
        {
            _currentPlaylist.Clear();
        }
        UpdateDuplicateFlags();
        await UpdatePositionsAsync();

        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
        {
            ChangeType = PlaylistChangeType.Cleared
        });
    }

    public async Task ShufflePlaylistAsync(int startIndex = 0)
    {
        startIndex = Math.Max(0, startIndex);
        if (_currentPlaylist.Count - startIndex <= 1)
            return;

        var random = new Random();
        var items = _currentPlaylist.ToList();

        // Fisher-Yates shuffle of items[startIndex..]
        for (int i = items.Count - 1; i > startIndex; i--)
        {
            int j = random.Next(startIndex, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        _currentPlaylist.Clear();
        foreach (var item in items)
        {
            _currentPlaylist.Add(item);
        }

        await UpdatePositionsAsync();

        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
        {
            ChangeType = PlaylistChangeType.Shuffled
        });
    }

    public bool IsDuplicate(MediaFile mediaFile)
    {
        if (mediaFile == null)
            return false;

        return _currentPlaylist.Any(item => item.MediaFileId == mediaFile.Id);
    }

    public async Task SavePlaylistAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".m3u" && extension != ".m3u8")
            throw new ArgumentException("File must have .m3u or .m3u8 extension", nameof(filePath));

        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");

        foreach (var item in _currentPlaylist)
        {
            if (item.MediaFile != null)
            {
                var duration = item.MediaFile.Metadata?.Duration ?? 0;
                var artist = item.MediaFile.Metadata?.Artist ?? "Unknown Artist";
                var title = item.MediaFile.Metadata?.Title ?? item.MediaFile.Filename;

                sb.AppendLine($"#EXTINF:{duration},{artist} - {title}");
                sb.AppendLine(item.MediaFile.FilePath);
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task LoadPlaylistAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Playlist file not found", filePath);

        var entries = await Task.Run(() => PlaylistFile.ReadEntries(filePath));

        List<MediaFile> library;
        using (var context = _dbContextFactory.CreateDbContext())
        {
            library = await context.MediaFiles.Include(m => m.Metadata).AsNoTracking().ToListAsync();
        }

        // Songs that are not in the library are left out (and counted): the queue
        // only holds playable songs, so its indices match what the UI shows
        _currentPlaylist.Clear();
        var missing = 0;
        foreach (var entry in entries)
        {
            var mediaFile = PlaylistFile.Resolve(entry, library);
            if (mediaFile == null)
            {
                missing++;
                continue;
            }

            _currentPlaylist.Add(new PlaylistItem
            {
                MediaFileId = mediaFile.Id,
                MediaFile = mediaFile,
                Position = _currentPlaylist.Count,
                AddedAt = DateTime.UtcNow,
                IsDuplicate = false
            });
        }
        LastLoadMissingCount = missing;

        // Update duplicate flags
        UpdateDuplicateFlags();

        await UpdatePositionsAsync();

        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
        {
            ChangeType = PlaylistChangeType.Loaded
        });
    }

    public async Task RestoreLastSessionAsync()
    {
        if (!File.Exists(_autoSaveFilePath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_autoSaveFilePath);
            var savedData = JsonSerializer.Deserialize<SavedPlaylistData>(json);

            if (savedData?.Items == null || savedData.Items.Count == 0)
                return;

            _currentPlaylist.Clear();

            using var context = _dbContextFactory.CreateDbContext();

            foreach (var savedItem in savedData.Items)
            {
                var mediaFile = await context.MediaFiles
                    .Include(m => m.Metadata)
                    .FirstOrDefaultAsync(m => m.Id == savedItem.MediaFileId);

                if (mediaFile != null)
                {
                    var playlistItem = new PlaylistItem
                    {
                        MediaFileId = mediaFile.Id,
                        MediaFile = mediaFile,
                        Position = savedItem.Position,
                        AddedAt = savedItem.AddedAt,
                        IsDuplicate = false
                    };

                    _currentPlaylist.Add(playlistItem);
                }
            }

            // Update duplicate flags
            UpdateDuplicateFlags();

            await UpdatePositionsAsync();

            PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
            {
                ChangeType = PlaylistChangeType.Loaded
            });
        }
        catch (Exception)
        {
            // If restore fails, just start with empty playlist
            _currentPlaylist.Clear();
        }
    }

    public List<PlaylistItem> GetCurrentPlaylist()
    {
        return _currentPlaylist.ToList();
    }

    private void TakeSnapshot()
    {
        lock (_snapshotLock)
        {
            _snapshot = _currentPlaylist.Select((item, index) => new SavedPlaylistItem
            {
                MediaFileId = item.MediaFileId,
                Position = index,
                AddedAt = item.AddedAt
            }).ToList();
            _revision++;
        }
    }

    private void ScheduleAutoSave()
    {
        // Debounce auto-save by 1 second
        lock (_snapshotLock)
        {
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = new Timer(_ => _ = AutoSaveAsync(), null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Writes the playlist now if it changed (called on exit: the debounce would lose the last edit).</summary>
    public Task FlushAsync() => AutoSaveAsync();

    private async Task AutoSaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            List<SavedPlaylistItem> items;
            long revision;
            lock (_snapshotLock)
            {
                if (_revision == _savedRevision)
                    return;
                items = _snapshot;
                revision = _revision;
            }

            var json = JsonSerializer.Serialize(new SavedPlaylistData { Items = items },
                new JsonSerializerOptions { WriteIndented = true });

            // Write a temporary file and replace: a failed write keeps the previous playlist
            var tempPath = _autoSaveFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _autoSaveFilePath, overwrite: true);

            lock (_snapshotLock)
            {
                _savedRevision = Math.Max(_savedRevision, revision);
            }
        }
        catch (Exception ex)
        {
            // Disk full, file locked...: keep the change pending, the next change retries
            Console.WriteLine($"Could not save the current playlist: {ex.Message}");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private Task UpdatePositionsAsync()
    {
        for (int i = 0; i < _currentPlaylist.Count; i++)
        {
            _currentPlaylist[i].Position = i;
        }
        return Task.CompletedTask;
    }

    private void UpdateDuplicateFlags()
    {
        var mediaFileIds = new HashSet<string>();

        foreach (var item in _currentPlaylist)
        {
            if (mediaFileIds.Contains(item.MediaFileId))
            {
                item.IsDuplicate = true;
            }
            else
            {
                item.IsDuplicate = false;
                mediaFileIds.Add(item.MediaFileId);
            }
        }
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        _saveLock?.Dispose();
    }
}

/// <summary>
/// Data structure for saving playlist to JSON
/// </summary>
internal class SavedPlaylistData
{
    public List<SavedPlaylistItem> Items { get; set; } = new();
}

/// <summary>
/// Simplified playlist item for JSON serialization
/// </summary>
internal class SavedPlaylistItem
{
    public string MediaFileId { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime AddedAt { get; set; }
}
