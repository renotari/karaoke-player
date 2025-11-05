using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using MediaInfo;
using Microsoft.EntityFrameworkCore;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for extracting metadata from media files
/// </summary>
public class MetadataExtractor : IMetadataExtractor, IDisposable
{
    private readonly KaraokeDbContext _dbContext;
    private readonly ConcurrentQueue<MediaFile> _extractionQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;
    private bool _isProcessing;

    public event EventHandler<MetadataExtractedEventArgs>? MetadataExtracted;
    public event EventHandler<MetadataExtractionFailedEventArgs>? MetadataExtractionFailed;

    public bool IsProcessing => _isProcessing;
    public int QueuedCount => _extractionQueue.Count;

    public MetadataExtractor(KaraokeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Extracts metadata from a video file using MediaInfo
    /// </summary>
    public async Task<MediaMetadata> ExtractVideoMetadataAsync(MediaFile mediaFile)
    {
        var metadata = new MediaMetadata
        {
            MediaFileId = mediaFile.Id
        };

        try
        {
            var fileInfo = new FileInfo(mediaFile.FilePath);
            metadata.FileSize = fileInfo.Length;

            var mediaInfo = new MediaInfoWrapper(mediaFile.FilePath);

            // Extract duration (in milliseconds, convert to seconds)
            var durationMs = mediaInfo.Duration;
            metadata.Duration = durationMs / 1000.0;

            // Extract resolution
            var width = mediaInfo.Width;
            var height = mediaInfo.Height;
            if (width > 0 && height > 0)
            {
                metadata.ResolutionWidth = width;
                metadata.ResolutionHeight = height;
            }

            // Check for subtitles - MediaInfo.Wrapper doesn't expose text stream count directly
            // We'll set this to false for now and can enhance later with direct MediaInfo library
            metadata.HasSubtitles = false;

            // Try to extract artist and title from video metadata
            // MediaInfo.Wrapper has limited metadata exposure, so we'll parse filename
            var parsed = ParseFilename(Path.GetFileNameWithoutExtension(mediaFile.Filename));
            metadata.Artist = parsed.Artist;
            metadata.Title = parsed.Title;
        }
        catch (Exception ex)
        {
            // Fallback to filename parsing on error
            var parsed = ParseFilename(Path.GetFileNameWithoutExtension(mediaFile.Filename));
            metadata.Artist = parsed.Artist;
            metadata.Title = parsed.Title;

            // Try to at least get file size
            try
            {
                var fileInfo = new FileInfo(mediaFile.FilePath);
                metadata.FileSize = fileInfo.Length;
            }
            catch
            {
                // Ignore file size errors
            }

            throw new Exception($"Failed to extract video metadata: {ex.Message}", ex);
        }

        return await Task.FromResult(metadata);
    }

    /// <summary>
    /// Extracts metadata from an audio file (MP3) using TagLib#
    /// </summary>
    public async Task<MediaMetadata> ExtractAudioMetadataAsync(MediaFile mediaFile)
    {
        var metadata = new MediaMetadata
        {
            MediaFileId = mediaFile.Id
        };

        try
        {
            var fileInfo = new FileInfo(mediaFile.FilePath);
            metadata.FileSize = fileInfo.Length;

            using var tagFile = TagLib.File.Create(mediaFile.FilePath);

            // Extract duration
            metadata.Duration = tagFile.Properties.Duration.TotalSeconds;

            // Extract ID3 tags
            var artist = tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist;
            var title = tagFile.Tag.Title;
            var album = tagFile.Tag.Album;

            // If ID3 tags not found, parse filename
            if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(title))
            {
                var parsed = ParseFilename(Path.GetFileNameWithoutExtension(mediaFile.Filename));
                metadata.Artist = parsed.Artist;
                metadata.Title = parsed.Title;
            }
            else
            {
                metadata.Artist = artist ?? string.Empty;
                metadata.Title = title ?? string.Empty;
            }

            metadata.Album = album;
        }
        catch (Exception ex)
        {
            // Fallback to filename parsing on error
            var parsed = ParseFilename(Path.GetFileNameWithoutExtension(mediaFile.Filename));
            metadata.Artist = parsed.Artist;
            metadata.Title = parsed.Title;

            // Try to at least get file size
            try
            {
                var fileInfo = new FileInfo(mediaFile.FilePath);
                metadata.FileSize = fileInfo.Length;
            }
            catch
            {
                // Ignore file size errors
            }

            throw new Exception($"Failed to extract audio metadata: {ex.Message}", ex);
        }

        return await Task.FromResult(metadata);
    }

    /// <summary>
    /// Parses filename to extract artist and title using multiple patterns
    /// Patterns: "Artist - Title", "Artist-Title", "Title (Artist)", "Artist_Title"
    /// </summary>
    public (string Artist, string Title) ParseFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return (string.Empty, string.Empty);
        }

        // Pattern 1: "Artist - Title" (with space around dash)
        var match = Regex.Match(filename, @"^(.+?)\s+-\s+(.+)$");
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
        }

        // Pattern 2: "Artist-Title" (no space around dash)
        match = Regex.Match(filename, @"^(.+?)-(.+)$");
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
        }

        // Pattern 3: "Title (Artist)"
        match = Regex.Match(filename, @"^(.+?)\s*\((.+?)\)$");
        if (match.Success)
        {
            return (match.Groups[2].Value.Trim(), match.Groups[1].Value.Trim());
        }

        // Pattern 4: "Artist_Title" (underscore separator)
        match = Regex.Match(filename, @"^(.+?)_(.+)$");
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
        }

        // No pattern matched - use entire filename as title, empty artist
        return (string.Empty, filename.Trim());
    }

    /// <summary>
    /// Queues a media file for background metadata extraction
    /// </summary>
    public void QueueForExtraction(MediaFile mediaFile)
    {
        _extractionQueue.Enqueue(mediaFile);
    }

    /// <summary>
    /// Starts the background processing queue
    /// </summary>
    public void StartProcessing()
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Stops the background processing queue
    /// </summary>
    public void StopProcessing()
    {
        if (!_isProcessing)
        {
            return;
        }

        _isProcessing = false;
        _cancellationTokenSource.Cancel();
        _processingTask?.Wait();
    }

    /// <summary>
    /// Background task that processes the extraction queue
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        while (_isProcessing && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_extractionQueue.TryDequeue(out var mediaFile))
            {
                try
                {
                    MediaMetadata metadata;

                    // Extract metadata based on file type
                    if (mediaFile.Type == MediaType.Video)
                    {
                        metadata = await ExtractVideoMetadataAsync(mediaFile);
                    }
                    else
                    {
                        metadata = await ExtractAudioMetadataAsync(mediaFile);
                    }

                    // Update database
                    await UpdateDatabaseAsync(mediaFile, metadata);

                    // Raise success event
                    MetadataExtracted?.Invoke(this, new MetadataExtractedEventArgs
                    {
                        MediaFile = mediaFile,
                        Metadata = metadata
                    });
                }
                catch (Exception ex)
                {
                    // Raise failure event
                    MetadataExtractionFailed?.Invoke(this, new MetadataExtractionFailedEventArgs
                    {
                        MediaFile = mediaFile,
                        ErrorMessage = ex.Message,
                        Exception = ex
                    });
                }
            }
            else
            {
                // Queue is empty, wait a bit before checking again
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
        }
    }

    /// <summary>
    /// Updates the database with extracted metadata
    /// </summary>
    private async Task UpdateDatabaseAsync(MediaFile mediaFile, MediaMetadata metadata)
    {
        // Check if metadata already exists
        var existingMetadata = await _dbContext.MediaMetadata
            .FirstOrDefaultAsync(m => m.MediaFileId == mediaFile.Id);

        if (existingMetadata != null)
        {
            // Update existing metadata
            existingMetadata.Duration = metadata.Duration;
            existingMetadata.Artist = metadata.Artist;
            existingMetadata.Title = metadata.Title;
            existingMetadata.Album = metadata.Album;
            existingMetadata.ResolutionWidth = metadata.ResolutionWidth;
            existingMetadata.ResolutionHeight = metadata.ResolutionHeight;
            existingMetadata.FileSize = metadata.FileSize;
            existingMetadata.HasSubtitles = metadata.HasSubtitles;

            _dbContext.MediaMetadata.Update(existingMetadata);
        }
        else
        {
            // Add new metadata
            await _dbContext.MediaMetadata.AddAsync(metadata);
        }

        // Update MediaFile to mark metadata as loaded
        var dbMediaFile = await _dbContext.MediaFiles.FindAsync(mediaFile.Id);
        if (dbMediaFile != null)
        {
            dbMediaFile.MetadataLoaded = true;
            _dbContext.MediaFiles.Update(dbMediaFile);
        }

        await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        StopProcessing();
        _cancellationTokenSource.Dispose();
    }
}
