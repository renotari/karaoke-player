using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Manages the media library including scanning, monitoring, and database operations
/// </summary>
public class MediaLibraryManager : IMediaLibraryManager, IDisposable
{
    private readonly IDbContextFactory _dbContextFactory;
    private FileSystemWatcher? _fileWatcher;
    private string? _monitoredDirectory;

    public string? MonitoredDirectory => _monitoredDirectory;
    private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mp3"
    };

    public event EventHandler<MediaFilesChangedEventArgs>? FilesAdded;
    public event EventHandler<MediaFilesChangedEventArgs>? FilesRemoved;
    public event EventHandler<MediaFilesChangedEventArgs>? FilesModified;
    public event EventHandler<ScanProgressEventArgs>? ScanProgress;

    public bool IsMonitoring => _fileWatcher?.EnableRaisingEvents ?? false;

    /// <summary>
    /// One change to the library at a time: scans, additions (watcher, YouTube
    /// downloads) and renames all go through this lock, so they never work on
    /// different snapshots of the database.
    /// </summary>
    private readonly SemaphoreSlim _libraryLock = new(1, 1);

    /// <summary>Folders the last scan could not read (their songs were kept).</summary>
    public IReadOnlyList<string> SkippedFolders { get; private set; } = Array.Empty<string>();

    // Watcher: changes are collected and reconciled by an incremental rescan
    private Timer? _resyncTimer;
    private readonly object _resyncLock = new();
    private static readonly TimeSpan ResyncDelay = TimeSpan.FromMilliseconds(1500);

    public MediaLibraryManager(IDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// Scans the specified directory recursively for media files (incremental:
    /// only new, changed and removed files touch the database).
    /// </summary>
    public async Task ScanDirectoryAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        await _libraryLock.WaitAsync();
        try
        {
            _monitoredDirectory = directoryPath;
            await ReconcileAsync(directoryPath);
        }
        finally
        {
            _libraryLock.Release();
        }
    }

    /// <summary>Brings the database in line with the folder. Must be called with _libraryLock held.</summary>
    private async Task ReconcileAsync(string directoryPath)
    {
        var (mediaFiles, skippedFolders) = FindMediaFiles(directoryPath);
        SkippedFolders = skippedFolders;
        var totalFiles = mediaFiles.Count;

        using var context = _dbContextFactory.CreateDbContext();

        // Get existing files from database
        var existingFiles = await context.MediaFiles
            .AsNoTracking()
            .ToDictionaryAsync(f => f.FilePath, f => f);

        var filesToAdd = new List<MediaFile>();
        var filesToUpdate = new List<MediaFile>();
        var processedCount = 0;

        foreach (var filePath in mediaFiles)
        {
            processedCount++;

            // Report progress
            OnScanProgress(new ScanProgressEventArgs
            {
                FilesProcessed = processedCount,
                TotalFiles = totalFiles,
                CurrentFile = Path.GetFileName(filePath)
            });

            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(filePath);
                _ = fileInfo.LastWriteTimeUtc; // the file may be gone already
            }
            catch (IOException)
            {
                continue;
            }
            var lastModified = fileInfo.LastWriteTimeUtc;

            if (existingFiles.TryGetValue(filePath, out var existingFile))
            {
                // Replaced content: metadata and thumbnail must be read again (also
                // after a restart, so the flags are saved, not only queued)
                if (existingFile.LastModified == null || existingFile.LastModified < lastModified)
                {
                    existingFile.LastModified = lastModified;
                    existingFile.Filename = fileInfo.Name;
                    existingFile.MetadataLoaded = false;
                    existingFile.ThumbnailLoaded = false;
                    filesToUpdate.Add(existingFile);
                }
            }
            else
            {
                // New file
                var mediaFile = CreateMediaFile(filePath, fileInfo);
                filesToAdd.Add(mediaFile);
            }
        }

        // Files that were removed. Songs inside folders that could not be read are
        // kept: a folder that is unreadable for a moment must not lose its songs
        // (their ids are referenced by the saved playlist).
        var currentFilePaths = new HashSet<string>(mediaFiles, StringComparer.OrdinalIgnoreCase);
        var skippedPrefixes = skippedFolders
            .Select(f => Path.TrimEndingDirectorySeparator(f) + Path.DirectorySeparatorChar)
            .ToList();
        var filesToRemove = existingFiles.Values
            .Where(f => !currentFilePaths.Contains(f.FilePath)
                        && !skippedPrefixes.Any(prefix => f.FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Update database
        if (filesToAdd.Any())
        {
            await context.MediaFiles.AddRangeAsync(filesToAdd);
            await context.SaveChangesAsync();
            OnFilesAdded(new MediaFilesChangedEventArgs { Files = filesToAdd });
        }

        if (filesToUpdate.Any())
        {
            context.MediaFiles.UpdateRange(filesToUpdate);
            await context.SaveChangesAsync();
            OnFilesModified(new MediaFilesChangedEventArgs { Files = filesToUpdate });
        }

        if (filesToRemove.Any())
        {
            context.MediaFiles.RemoveRange(filesToRemove);
            await context.SaveChangesAsync();
            OnFilesRemoved(new MediaFilesChangedEventArgs { Files = filesToRemove });
        }
    }

    /// <summary>
    /// Gets all media files from the database
    /// </summary>
    public async Task<List<MediaFile>> GetMediaFilesAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();

        return await context.MediaFiles
            .Include(f => f.Metadata)
            .OrderBy(f => f.Filename)
            .ToListAsync();
    }

    /// <summary>
    /// Starts monitoring the media directory for changes
    /// </summary>
    public void StartMonitoring()
    {
        if (string.IsNullOrWhiteSpace(_monitoredDirectory))
            throw new InvalidOperationException("No directory has been scanned yet. Call ScanDirectoryAsync first.");

        if (!Directory.Exists(_monitoredDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {_monitoredDirectory}");

        StopMonitoring();

        _fileWatcher = new FileSystemWatcher(_monitoredDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            InternalBufferSize = 64 * 1024
        };

        _fileWatcher.Created += OnFileSystemChanged;
        _fileWatcher.Deleted += OnFileSystemChanged;
        _fileWatcher.Changed += OnFileSystemChanged;
        _fileWatcher.Renamed += OnFileRenamed;
        _fileWatcher.Error += OnWatcherError;

        _fileWatcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Stops monitoring the media directory for changes
    /// </summary>
    public void StopMonitoring()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Created -= OnFileSystemChanged;
            _fileWatcher.Deleted -= OnFileSystemChanged;
            _fileWatcher.Changed -= OnFileSystemChanged;
            _fileWatcher.Renamed -= OnFileRenamed;
            _fileWatcher.Error -= OnWatcherError;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        lock (_resyncLock)
        {
            _resyncTimer?.Dispose();
            _resyncTimer = null;
        }
    }

    /// <summary>
    /// Lists the media files under the folder. Folders that cannot be read
    /// (System Volume Information, the "My Music" junction in Documents...) are
    /// skipped and returned, so their songs are not taken as deleted. Hidden and
    /// system items (the recycle bin) and links to other folders are not entered.
    /// The root folder itself must be readable: that error reaches the caller.
    /// </summary>
    private (List<string> Files, List<string> Skipped) FindMediaFiles(string directoryPath)
    {
        var files = new List<string>();
        var skipped = new List<string>();
        var pending = new Stack<string>();
        pending.Push(directoryPath);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            List<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(directory).EnumerateFileSystemInfos().ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException
                                       && !string.Equals(directory, directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(directory);
                continue;
            }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try { attributes = entry.Attributes; }
                catch (IOException) { continue; }

                if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    continue;

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                        pending.Push(entry.FullName);
                }
                else if (IsSupportedMediaFile(entry.FullName))
                {
                    files.Add(entry.FullName);
                }
            }
        }

        return (files, skipped);
    }

    private bool IsSupportedMediaFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return _supportedExtensions.Contains(extension);
    }

    private MediaFile CreateMediaFile(string filePath, FileInfo fileInfo)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var format = extension switch
        {
            ".mp4" => MediaFormat.MP4,
            ".mkv" => MediaFormat.MKV,
            ".webm" => MediaFormat.WEBM,
            ".mp3" => MediaFormat.MP3,
            _ => throw new NotSupportedException($"Unsupported file format: {extension}")
        };

        var type = extension == ".mp3" ? MediaType.Audio : MediaType.Video;

        return new MediaFile
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = filePath,
            Filename = fileInfo.Name,
            Type = type,
            Format = format,
            MetadataLoaded = false,
            ThumbnailLoaded = false,
            LastModified = fileInfo.LastWriteTimeUtc,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adds one file to the library (raising FilesAdded) and returns it; when the
    /// path is already in the library the existing entry is returned. Used by the
    /// YouTube download (the watcher's rescan may see the same file).
    /// </summary>
    public async Task<MediaFile?> AddFileAsync(string filePath)
    {
        if (!IsSupportedMediaFile(filePath) || !File.Exists(filePath))
            return null;

        MediaFile mediaFile;
        await _libraryLock.WaitAsync();
        try
        {
            using var context = _dbContextFactory.CreateDbContext();
            var existing = await context.MediaFiles.FirstOrDefaultAsync(f => f.FilePath == filePath);
            if (existing != null)
                return existing;

            mediaFile = CreateMediaFile(filePath, new FileInfo(filePath));
            await context.MediaFiles.AddAsync(mediaFile);
            await context.SaveChangesAsync();
        }
        finally
        {
            _libraryLock.Release();
        }

        OnFilesAdded(new MediaFilesChangedEventArgs { Files = new List<MediaFile> { mediaFile } });
        return mediaFile;
    }

    // ----- Watcher ---------------------------------------------------------------
    //
    // Created/deleted/changed files and folders (a folder moved in or out has no
    // event per file), and watcher overflows, are all handled the same way: after
    // a short quiet time the folder is rescanned. The rescan is incremental, so
    // only what changed touches the database, and a file still being copied is
    // picked up again by the next change. Renames of media files keep the song's
    // id (playlist and metadata) and are applied directly.

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        // Other files (covers, lyrics, temporary files) do not matter; a path
        // without extension can be a folder
        var extension = Path.GetExtension(e.FullPath);
        if (!string.IsNullOrEmpty(extension) && !_supportedExtensions.Contains(extension))
            return;

        ScheduleResync();
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // Usually a buffer overflow (many changes at once): events were lost
        Console.WriteLine($"File watcher error: {e.GetException().Message}");
        ScheduleResync();
    }

    private void ScheduleResync()
    {
        lock (_resyncLock)
        {
            _resyncTimer?.Dispose();
            _resyncTimer = new Timer(_ => _ = ResyncAsync(), null, ResyncDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task ResyncAsync()
    {
        var directory = _monitoredDirectory;
        if (directory == null || !Directory.Exists(directory))
            return;

        await _libraryLock.WaitAsync();
        try
        {
            // The folder may have changed while waiting
            if (directory == _monitoredDirectory)
                await ReconcileAsync(directory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating the library after a folder change: {ex.Message}");
        }
        finally
        {
            _libraryLock.Release();
        }

        // After an error the watcher may have stopped
        if (_fileWatcher != null && !_fileWatcher.EnableRaisingEvents)
        {
            try { StartMonitoring(); } catch (Exception ex) { Console.WriteLine($"Could not restart the file watcher: {ex.Message}"); }
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _ = HandleFileRenamedAsync(e);
    }

    private async Task HandleFileRenamedAsync(RenamedEventArgs e)
    {
        // Folder renamed, or a file becoming / no longer being a media file
        // ("song.tmp" -> "song.mp4"): the rescan sorts it out
        if (!IsSupportedMediaFile(e.OldFullPath) || !IsSupportedMediaFile(e.FullPath) || Directory.Exists(e.FullPath))
        {
            ScheduleResync();
            return;
        }

        await _libraryLock.WaitAsync();
        try
        {
            using var context = _dbContextFactory.CreateDbContext();

            var mediaFile = await context.MediaFiles
                .FirstOrDefaultAsync(f => f.FilePath == e.OldFullPath);

            if (mediaFile == null)
            {
                ScheduleResync();
                return;
            }

            mediaFile.FilePath = e.FullPath;
            mediaFile.Filename = Path.GetFileName(e.FullPath);
            mediaFile.LastModified = File.GetLastWriteTimeUtc(e.FullPath);

            context.MediaFiles.Update(mediaFile);
            await context.SaveChangesAsync();

            OnFilesModified(new MediaFilesChangedEventArgs { Files = new List<MediaFile> { mediaFile } });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling file renamed from '{e.OldFullPath}' to '{e.FullPath}': {ex.Message}");
            ScheduleResync();
        }
        finally
        {
            _libraryLock.Release();
        }
    }

    protected virtual void OnFilesAdded(MediaFilesChangedEventArgs e)
    {
        FilesAdded?.Invoke(this, e);
    }

    protected virtual void OnFilesRemoved(MediaFilesChangedEventArgs e)
    {
        FilesRemoved?.Invoke(this, e);
    }

    protected virtual void OnFilesModified(MediaFilesChangedEventArgs e)
    {
        FilesModified?.Invoke(this, e);
    }

    protected virtual void OnScanProgress(ScanProgressEventArgs e)
    {
        ScanProgress?.Invoke(this, e);
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
