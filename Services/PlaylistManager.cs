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
    private readonly KaraokeDbContext _dbContext;
    private readonly string _autoSaveFilePath;
    private readonly ObservableCollection<PlaylistItem> _currentPlaylist;
    private Timer? _autoSaveTimer;
    private bool _isDirty;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ObservableCollection<PlaylistItem> CurrentPlaylist => _currentPlaylist;

    public event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;

    public PlaylistManager(KaraokeDbContext dbContext)
    {
        _dbContext = dbContext;
        
        // Set up auto-save file path in user data directory
        var userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KaraokePlayer"
        );
        Directory.CreateDirectory(userDataPath);
        _autoSaveFilePath = Path.Combine(userDataPath, "current-playlist.json");

        _currentPlaylist = new ObservableCollection<PlaylistItem>();
        _currentPlaylist.CollectionChanged += (s, e) =>
        {
            _isDirty = true;
            ScheduleAutoSave();
        };
    }

    public async Task AddSongAsync(MediaFile mediaFile, string position = "next")
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        if (position != "next" && position != "end")
            throw new ArgumentException("Position must be 'next' or 'end'", nameof(position));

        // Check if it's a duplicate
        var isDuplicate = IsDuplicate(mediaFile);

        // Determine insertion index
        int insertIndex;
        if (position == "next")
        {
            // Find the currently playing song (position 0) and insert after it
            insertIndex = _currentPlaylist.Count > 0 ? 1 : 0;
        }
        else // "end"
        {
            insertIndex = _currentPlaylist.Count;
        }

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

    public async Task ClearPlaylistAsync()
    {
        _currentPlaylist.Clear();
        await UpdatePositionsAsync();

        PlaylistChanged?.Invoke(this, new PlaylistChangedEventArgs
        {
            ChangeType = PlaylistChangeType.Cleared
        });
    }

    public async Task ShufflePlaylistAsync()
    {
        if (_currentPlaylist.Count <= 1)
            return;

        var random = new Random();
        var items = _currentPlaylist.ToList();

        // Fisher-Yates shuffle
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
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

        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        var filePaths = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and comments (except #EXTM3U header)
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            filePaths.Add(trimmedLine);
        }

        // Clear current playlist
        _currentPlaylist.Clear();

        // Load media files from database
        foreach (var path in filePaths)
        {
            var mediaFile = await _dbContext.MediaFiles
                .Include(m => m.Metadata)
                .FirstOrDefaultAsync(m => m.FilePath == path);

            if (mediaFile != null)
            {
                var playlistItem = new PlaylistItem
                {
                    MediaFileId = mediaFile.Id,
                    MediaFile = mediaFile,
                    Position = _currentPlaylist.Count,
                    AddedAt = DateTime.UtcNow,
                    IsDuplicate = false
                };

                _currentPlaylist.Add(playlistItem);
            }
            else
            {
                // File not found in database - mark as error
                var errorItem = new PlaylistItem
                {
                    MediaFileId = Guid.NewGuid().ToString(),
                    Position = _currentPlaylist.Count,
                    AddedAt = DateTime.UtcNow,
                    IsDuplicate = false,
                    Error = $"File not found: {path}"
                };

                _currentPlaylist.Add(errorItem);
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

            foreach (var savedItem in savedData.Items)
            {
                var mediaFile = await _dbContext.MediaFiles
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

    private void ScheduleAutoSave()
    {
        // Debounce auto-save by 1 second
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = new Timer(async _ => await AutoSaveAsync(), null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
    }

    private async Task AutoSaveAsync()
    {
        if (!_isDirty)
            return;

        await _saveLock.WaitAsync();
        try
        {
            var savedData = new SavedPlaylistData
            {
                Items = _currentPlaylist.Select(item => new SavedPlaylistItem
                {
                    MediaFileId = item.MediaFileId,
                    Position = item.Position,
                    AddedAt = item.AddedAt
                }).ToList()
            };

            var json = JsonSerializer.Serialize(savedData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_autoSaveFilePath, json);
            _isDirty = false;
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
