using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using LibVLCSharp.Shared;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for generating thumbnails from media files
/// </summary>
public class ThumbnailGenerator : IThumbnailGenerator, IDisposable
{
    private readonly IDbContextFactory _dbContextFactory;
    private readonly LibVLC _libVLC;
    private readonly ConcurrentQueue<MediaFile> _generationQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;
    private bool _isProcessing;
    private readonly string _cacheDirectory;

    // Thumbnail dimensions
    private const int ThumbnailWidth = 320;
    private const int ThumbnailHeight = 180;

    public event EventHandler<ThumbnailGeneratedEventArgs>? ThumbnailGenerated;
    public event EventHandler<ThumbnailGenerationFailedEventArgs>? ThumbnailGenerationFailed;

    public bool IsProcessing => _isProcessing;
    public int QueuedCount => _generationQueue.Count;
    public string CacheDirectory => _cacheDirectory;

    public ThumbnailGenerator(IDbContextFactory dbContextFactory, LibVLC libVLC)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _libVLC = libVLC;

        // Set up cache directory in user's app data folder
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appDataPath, "KaraokePlayer", "Thumbnails");

        // Create cache directory if it doesn't exist
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Generates a thumbnail from a video file by capturing a frame at 10% duration
    /// </summary>
    public async Task<string> GenerateVideoThumbnailAsync(MediaFile mediaFile)
    {
        var thumbnailPath = GetThumbnailPath(mediaFile.Id, "jpg");

        try
        {
            // Create a media player for thumbnail extraction
            using var mediaPlayer = new MediaPlayer(_libVLC);
            using var media = new Media(_libVLC, mediaFile.FilePath, FromType.FromPath);

            // Parse media to get duration
            await media.Parse(MediaParseOptions.ParseNetwork);

            var duration = media.Duration;
            if (duration <= 0)
            {
                throw new Exception("Could not determine video duration");
            }

            // Calculate 10% position
            var capturePosition = (long)(duration * 0.1);

            mediaPlayer.Media = media;
            mediaPlayer.Play();

            // Wait for player to start
            await Task.Delay(100);

            // Seek to 10% position
            mediaPlayer.Time = capturePosition;
            await Task.Delay(300); // Wait for seek to complete and frame to render

            // Take snapshot - returns 0 on success, -1 on error
            var result = mediaPlayer.TakeSnapshot(0, thumbnailPath, (uint)ThumbnailWidth, (uint)ThumbnailHeight);
            
            mediaPlayer.Stop();

            // Wait a bit for the snapshot file to be written
            await Task.Delay(500);

            // Verify the file was created
            if (!File.Exists(thumbnailPath))
            {
                throw new Exception("Thumbnail file was not created");
            }

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            // Clean up partial file if it exists
            if (File.Exists(thumbnailPath))
            {
                try
                {
                    File.Delete(thumbnailPath);
                }
                catch (Exception deleteEx)
                {
                    Console.WriteLine($"Warning: Failed to delete partial thumbnail {thumbnailPath}: {deleteEx.Message}");
                }
            }

            throw new Exception($"Failed to generate video thumbnail: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts embedded artwork from an MP3 file using TagLib#
    /// </summary>
    public async Task<string?> ExtractAudioArtworkAsync(MediaFile mediaFile)
    {
        try
        {
            using var tagFile = TagLib.File.Create(mediaFile.FilePath);

            // Check if the file has embedded pictures
            var pictures = tagFile.Tag.Pictures;
            if (pictures == null || pictures.Length == 0)
            {
                return null;
            }

            // Get the first picture (usually the album art)
            var picture = pictures[0];
            var thumbnailPath = GetThumbnailPath(mediaFile.Id, GetImageExtension(picture.MimeType));

            // Save the artwork to file
            await File.WriteAllBytesAsync(thumbnailPath, picture.Data.Data);

            // Resize the image to thumbnail size using SkiaSharp
            await ResizeImageAsync(thumbnailPath, ThumbnailWidth, ThumbnailHeight);

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to extract audio artwork: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a default placeholder thumbnail for files without artwork
    /// </summary>
    public string CreateDefaultThumbnail(Models.MediaType mediaType)
    {
        var placeholderName = mediaType == Models.MediaType.Video ? "video_placeholder.png" : "audio_placeholder.png";
        var placeholderPath = Path.Combine(_cacheDirectory, placeholderName);

        // Check if placeholder already exists
        if (File.Exists(placeholderPath))
        {
            return placeholderPath;
        }

        // Create a simple placeholder image using SkiaSharp
        using var surface = SKSurface.Create(new SKImageInfo(ThumbnailWidth, ThumbnailHeight));
        var canvas = surface.Canvas;

        // Background
        canvas.Clear(SKColors.DarkGray);

        // Icon/text
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        using var font = new SKFont
        {
            Size = 24
        };

        var text = mediaType == Models.MediaType.Video ? "VIDEO" : "AUDIO";
        var x = ThumbnailWidth / 2f;
        var y = ThumbnailHeight / 2f + 8;

        canvas.DrawText(text, x, y, SKTextAlign.Center, font, paint);

        // Save to file
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        using var stream = File.OpenWrite(placeholderPath);
        data.SaveTo(stream);

        return placeholderPath;
    }

    /// <summary>
    /// Queues a media file for background thumbnail generation
    /// </summary>
    public void QueueForGeneration(MediaFile mediaFile)
    {
        _generationQueue.Enqueue(mediaFile);
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

        // Don't block waiting for task - let it complete naturally
        // The cancellation token will signal it to stop processing
    }

    /// <summary>
    /// Background task that processes the generation queue
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        while (_isProcessing && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_generationQueue.TryDequeue(out var mediaFile))
            {
                try
                {
                    string? thumbnailPath = null;

                    // Generate thumbnail based on file type
                    if (mediaFile.Type == Models.MediaType.Video)
                    {
                        thumbnailPath = await GenerateVideoThumbnailAsync(mediaFile);
                    }
                    else if (mediaFile.Type == Models.MediaType.Audio)
                    {
                        // Try to extract artwork first
                        thumbnailPath = await ExtractAudioArtworkAsync(mediaFile);

                        // If no artwork found, use placeholder
                        if (thumbnailPath == null)
                        {
                            thumbnailPath = CreateDefaultThumbnail(Models.MediaType.Audio);
                        }
                    }

                    // Update database
                    if (thumbnailPath != null)
                    {
                        await UpdateDatabaseAsync(mediaFile, thumbnailPath);

                        // Raise success event
                        ThumbnailGenerated?.Invoke(this, new ThumbnailGeneratedEventArgs
                        {
                            MediaFile = mediaFile,
                            ThumbnailPath = thumbnailPath
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Use placeholder on error
                    try
                    {
                        var placeholderPath = CreateDefaultThumbnail(mediaFile.Type);
                        await UpdateDatabaseAsync(mediaFile, placeholderPath);
                    }
                    catch
                    {
                        // Ignore placeholder creation errors
                    }

                    // Raise failure event
                    ThumbnailGenerationFailed?.Invoke(this, new ThumbnailGenerationFailedEventArgs
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
    /// Updates the database with the thumbnail path
    /// </summary>
    private async Task UpdateDatabaseAsync(MediaFile mediaFile, string thumbnailPath)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var dbMediaFile = await context.MediaFiles.FindAsync(mediaFile.Id);
        if (dbMediaFile != null)
        {
            dbMediaFile.ThumbnailPath = thumbnailPath;
            dbMediaFile.ThumbnailLoaded = true;
            context.MediaFiles.Update(dbMediaFile);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets the thumbnail file path for a media file
    /// </summary>
    private string GetThumbnailPath(string mediaFileId, string extension)
    {
        return Path.Combine(_cacheDirectory, $"{mediaFileId}.{extension}");
    }

    /// <summary>
    /// Gets the file extension for an image MIME type
    /// </summary>
    private string GetImageExtension(string mimeType)
    {
        return mimeType.ToLower() switch
        {
            "image/jpeg" or "image/jpg" => "jpg",
            "image/png" => "png",
            "image/gif" => "gif",
            "image/bmp" => "bmp",
            _ => "jpg" // Default to jpg
        };
    }

    /// <summary>
    /// Resizes an image to the specified dimensions while maintaining aspect ratio
    /// </summary>
    private async Task ResizeImageAsync(string imagePath, int maxWidth, int maxHeight)
    {
        await Task.Run(() =>
        {
            using var inputStream = File.OpenRead(imagePath);
            using var original = SKBitmap.Decode(inputStream);

            // Calculate new dimensions maintaining aspect ratio
            var ratioX = (double)maxWidth / original.Width;
            var ratioY = (double)maxHeight / original.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(original.Width * ratio);
            var newHeight = (int)(original.Height * ratio);

            // Create resized image
            var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), samplingOptions);
            if (resized == null)
            {
                throw new Exception("Failed to resize image");
            }

            // Save back to file
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            using var outputStream = File.OpenWrite(imagePath);
            outputStream.SetLength(0); // Clear existing content
            data.SaveTo(outputStream);
        });
    }

    public void Dispose()
    {
        StopProcessing();
        _cancellationTokenSource.Dispose();
    }
}
