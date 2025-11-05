using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Manages the media library including scanning, monitoring, and database operations
/// </summary>
public class MediaLibraryManager : IMediaLibraryManager, IDisposable
{
    private readonly KaraokeDbContext _dbContext;
    private FileSystemWatcher? _fileWatcher;
    private string? _monitoredDirectory;
    private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mp3"
    };

    public event EventHandler<MediaFilesChangedEventArgs>? FilesAdded;
    public event EventHandler<MediaFilesChangedEventArgs>? FilesRemoved;
    public event EventHandler<MediaFilesChangedEventArgs>? FilesModified;
    public event EventHandler<ScanProgressEventArgs>? ScanProgress;

    public bool IsMonitoring => _fileWatcher?.EnableRaisingEvents ?? false;

    public MediaLibraryManager(KaraokeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Scans the specified directory recursively for media files
    /// </summary>
    public async Task ScanDirectoryAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        _monitoredDirectory = directoryPath;

        // Find all media files recursively
        var mediaFiles = FindMediaFiles(directoryPath);
        var totalFiles = mediaFiles.Count;

        // Get existing files from database
        var existingFiles = await _dbContext.MediaFiles
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

            var fileInfo = new FileInfo(filePath);
            var lastModified = fileInfo.LastWriteTimeUtc;

            if (existingFiles.TryGetValue(filePath, out var existingFile))
            {
                // Check if file was modified
                if (existingFile.LastModified == null || existingFile.LastModified < lastModified)
                {
                    existingFile.LastModified = lastModified;
                    existingFile.Filename = fileInfo.Name;
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

        // Find files that were removed
        var currentFilePaths = new HashSet<string>(mediaFiles, StringComparer.OrdinalIgnoreCase);
        var filesToRemove = existingFiles.Values
            .Where(f => !currentFilePaths.Contains(f.FilePath))
            .ToList();

        // Update database
        if (filesToAdd.Any())
        {
            await _dbContext.MediaFiles.AddRangeAsync(filesToAdd);
            await _dbContext.SaveChangesAsync();
            OnFilesAdded(new MediaFilesChangedEventArgs { Files = filesToAdd });
        }

        if (filesToUpdate.Any())
        {
            _dbContext.MediaFiles.UpdateRange(filesToUpdate);
            await _dbContext.SaveChangesAsync();
            OnFilesModified(new MediaFilesChangedEventArgs { Files = filesToUpdate });
        }

        if (filesToRemove.Any())
        {
            _dbContext.MediaFiles.RemoveRange(filesToRemove);
            await _dbContext.SaveChangesAsync();
            OnFilesRemoved(new MediaFilesChangedEventArgs { Files = filesToRemove });
        }
    }

    /// <summary>
    /// Gets all media files from the database
    /// </summary>
    public async Task<List<MediaFile>> GetMediaFilesAsync()
    {
        return await _dbContext.MediaFiles
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
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _fileWatcher.Created += OnFileCreated;
        _fileWatcher.Deleted += OnFileDeleted;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Renamed += OnFileRenamed;

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
            _fileWatcher.Created -= OnFileCreated;
            _fileWatcher.Deleted -= OnFileDeleted;
            _fileWatcher.Changed -= OnFileChanged;
            _fileWatcher.Renamed -= OnFileRenamed;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }

    private List<string> FindMediaFiles(string directoryPath)
    {
        var mediaFiles = new List<string>();

        try
        {
            var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
            mediaFiles.AddRange(files.Where(f => IsSupportedMediaFile(f)));
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we don't have access to
        }
        catch (DirectoryNotFoundException)
        {
            // Skip directories that don't exist
        }

        return mediaFiles;
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

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedMediaFile(e.FullPath))
            return;

        try
        {
            // Wait a bit to ensure file is fully written
            await Task.Delay(500);

            if (!File.Exists(e.FullPath))
                return;

            var fileInfo = new FileInfo(e.FullPath);
            var mediaFile = CreateMediaFile(e.FullPath, fileInfo);

            await _dbContext.MediaFiles.AddAsync(mediaFile);
            await _dbContext.SaveChangesAsync();

            OnFilesAdded(new MediaFilesChangedEventArgs { Files = new List<MediaFile> { mediaFile } });
        }
        catch (Exception)
        {
            // Log error but don't crash
        }
    }

    private async void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedMediaFile(e.FullPath))
            return;

        try
        {
            var mediaFile = await _dbContext.MediaFiles
                .FirstOrDefaultAsync(f => f.FilePath == e.FullPath);

            if (mediaFile != null)
            {
                _dbContext.MediaFiles.Remove(mediaFile);
                await _dbContext.SaveChangesAsync();

                OnFilesRemoved(new MediaFilesChangedEventArgs { Files = new List<MediaFile> { mediaFile } });
            }
        }
        catch (Exception)
        {
            // Log error but don't crash
        }
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedMediaFile(e.FullPath))
            return;

        try
        {
            // Wait a bit to ensure file is fully written
            await Task.Delay(500);

            if (!File.Exists(e.FullPath))
                return;

            var mediaFile = await _dbContext.MediaFiles
                .FirstOrDefaultAsync(f => f.FilePath == e.FullPath);

            if (mediaFile != null)
            {
                var fileInfo = new FileInfo(e.FullPath);
                mediaFile.LastModified = fileInfo.LastWriteTimeUtc;
                mediaFile.Filename = fileInfo.Name;
                mediaFile.MetadataLoaded = false; // Mark for re-extraction
                mediaFile.ThumbnailLoaded = false;

                _dbContext.MediaFiles.Update(mediaFile);
                await _dbContext.SaveChangesAsync();

                OnFilesModified(new MediaFilesChangedEventArgs { Files = new List<MediaFile> { mediaFile } });
            }
        }
        catch (Exception)
        {
            // Log error but don't crash
        }
    }

    private async void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsSupportedMediaFile(e.FullPath))
            return;

        try
        {
            var mediaFile = await _dbContext.MediaFiles
                .FirstOrDefaultAsync(f => f.FilePath == e.OldFullPath);

            if (mediaFile != null)
            {
                mediaFile.FilePath = e.FullPath;
                mediaFile.Filename = Path.GetFileName(e.FullPath);
                mediaFile.LastModified = DateTime.UtcNow;

                _dbContext.MediaFiles.Update(mediaFile);
                await _dbContext.SaveChangesAsync();

                OnFilesModified(new MediaFilesChangedEventArgs { Files = new List<MediaFile> { mediaFile } });
            }
        }
        catch (Exception)
        {
            // Log error but don't crash
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
