using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using Microsoft.EntityFrameworkCore;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for extracting metadata from media files
/// </summary>
public class MetadataExtractor : IMetadataExtractor, IDisposable
{
    private readonly IDbContextFactory _dbContextFactory;
    private readonly ConcurrentQueue<MediaFile> _extractionQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;
    private bool _isProcessing;

    public event EventHandler<MetadataExtractedEventArgs>? MetadataExtracted;
    public event EventHandler<MetadataExtractionFailedEventArgs>? MetadataExtractionFailed;

    public bool IsProcessing => _isProcessing;
    public int QueuedCount => _extractionQueue.Count;

    public MetadataExtractor(IDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// Extracts metadata from a video file (MP4, MKV, WEBM) using TagLib#.
    /// Artist/title fall back to the file name when the container has no tags.
    /// </summary>
    public async Task<MediaMetadata> ExtractVideoMetadataAsync(MediaFile mediaFile)
    {
        var metadata = new MediaMetadata
        {
            MediaFileId = mediaFile.Id
        };

        // File name is the fallback for artist/title in every case
        var parsed = ParseFilename(Path.GetFileNameWithoutExtension(mediaFile.Filename));
        metadata.Artist = parsed.Artist;
        metadata.Title = parsed.Title;

        try
        {
            metadata.FileSize = new FileInfo(mediaFile.FilePath).Length;

            using var tagFile = TagLib.File.Create(mediaFile.FilePath);

            metadata.Duration = tagFile.Properties.Duration.TotalSeconds;

            if (tagFile.Properties.VideoWidth > 0 && tagFile.Properties.VideoHeight > 0)
            {
                metadata.ResolutionWidth = tagFile.Properties.VideoWidth;
                metadata.ResolutionHeight = tagFile.Properties.VideoHeight;
            }

            var tagArtist = tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist;
            var tagTitle = tagFile.Tag.Title;

            // Use container tags only when both are present: a lone title tag is
            // often the file name or an encoder string
            if (!string.IsNullOrWhiteSpace(tagArtist) && !string.IsNullOrWhiteSpace(tagTitle))
            {
                metadata.Artist = tagArtist.Trim();
                metadata.Title = tagTitle.Trim();
            }
        }
        catch (Exception)
        {
            // Unreadable container: keep file size (if any) and the file-name fallback.
            // Playback does not depend on this metadata.
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
        => FilenameParser.Parse(filename);

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

        // Let the item in progress finish (bounded wait), so nothing touches the
        // database or native resources after Dispose. The worker runs on the thread
        // pool, so waiting here cannot deadlock the UI thread.
        try { _processingTask?.Wait(TimeSpan.FromSeconds(3)); }
        catch (AggregateException) { /* cancelled or failed: it is over either way */ }
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
        using var context = _dbContextFactory.CreateDbContext();

        // Check if metadata already exists
        var existingMetadata = await context.MediaMetadata
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

            context.MediaMetadata.Update(existingMetadata);
        }
        else
        {
            // Add new metadata
            await context.MediaMetadata.AddAsync(metadata);
        }

        // Update MediaFile to mark metadata as loaded
        var dbMediaFile = await context.MediaFiles.FindAsync(mediaFile.Id);
        if (dbMediaFile != null)
        {
            dbMediaFile.MetadataLoaded = true;
            context.MediaFiles.Update(dbMediaFile);
        }

        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        StopProcessing();
        _cancellationTokenSource.Dispose();
    }
}
