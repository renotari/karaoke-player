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
    private const int FramesToSkip = 3;
    private const int MaxFramesToWait = 75;

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
        _cacheDirectory = AppPaths.Thumbnails;

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
            using var media = new Media(_libVLC, mediaFile.FilePath, FromType.FromPath);
            await media.Parse(MediaParseOptions.ParseLocal, timeout: 5000);

            var duration = media.Duration;
            if (duration <= 0)
            {
                throw new Exception("Could not determine video duration");
            }

            var (width, height) = GetThumbnailSize(media);

            // Capture at 10% of the duration (skips black intros), without audio
            var startSeconds = duration * 0.1 / 1000.0;
            var frame = await CaptureFrameAsync(mediaFile.FilePath, startSeconds, width, height);

            // Files with few keyframes can decode as plain grey after the seek:
            // fall back to the beginning of the video
            if (frame == null || !frame.Value.HasContent)
            {
                var fromStart = await CaptureFrameAsync(mediaFile.FilePath, 0, width, height);
                if (fromStart != null && (frame == null || fromStart.Value.HasContent))
                {
                    frame = fromStart;
                }
            }

            if (frame == null)
            {
                throw new Exception("No video frame decoded");
            }

            SaveJpeg(frame.Value.Pixels, (int)width, (int)height, thumbnailPath);

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
    /// Thumbnail size that fits ThumbnailWidth x ThumbnailHeight keeping the video aspect ratio.
    /// </summary>
    private static (uint Width, uint Height) GetThumbnailSize(Media media)
    {
        foreach (var track in media.Tracks)
        {
            if (track.TrackType == TrackType.Video && track.Data.Video.Width > 0 && track.Data.Video.Height > 0)
            {
                var video = track.Data.Video;
                double w = video.Width, h = video.Height;
                if (video.SarNum > 0 && video.SarDen > 0)
                {
                    w = w * video.SarNum / video.SarDen;
                }

                var scale = Math.Min(ThumbnailWidth / w, ThumbnailHeight / h);
                // Even sizes are safer for the video scaler
                var tw = Math.Max(2, (uint)(w * scale) & ~1u);
                var th = Math.Max(2, (uint)(h * scale) & ~1u);
                return (tw, th);
            }
        }

        return (ThumbnailWidth, ThumbnailHeight);
    }

    /// <summary>
    /// Decodes the media off-screen (LibVLC video callbacks: no window is created)
    /// and returns a copy of an early frame as BGRA pixels.
    /// </summary>
    private async Task<(byte[] Pixels, bool HasContent)?> CaptureFrameAsync(string filePath, double startSeconds, uint width, uint height)
    {
        using var media = new Media(_libVLC, filePath, FromType.FromPath);
        media.AddOption(":no-audio");
        if (startSeconds > 0)
        {
            media.AddOption($":start-time={startSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var pitch = width * 4;
        var bufferSize = (int)(pitch * height);
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bufferSize);
        var frameReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var framesDisplayed = 0;

        // Keep the delegates referenced for the whole decode (they are called from native code)
        MediaPlayer.LibVLCVideoLockCb lockCb = (opaque, planes) =>
        {
            System.Runtime.InteropServices.Marshal.WriteIntPtr(planes, buffer);
            return IntPtr.Zero;
        };
        MediaPlayer.LibVLCVideoDisplayCb displayCb = (opaque, picture) =>
        {
            // Right after the start-time seek, frames can be uniform grey until the
            // next keyframe: wait for a frame with some content (or give up after a while
            // and keep the last one: the video itself may be a plain background).
            var count = Interlocked.Increment(ref framesDisplayed);
            if (count >= FramesToSkip && (HasContent(buffer, width, height, pitch) || count >= MaxFramesToWait))
            {
                frameReady.TrySetResult(true);
            }
        };

        try
        {
            using var mediaPlayer = new MediaPlayer(media);
            mediaPlayer.SetVideoFormat("RV32", width, height, pitch);
            mediaPlayer.SetVideoCallbacks(lockCb, null, displayCb);
            mediaPlayer.EndReached += (_, _) => frameReady.TrySetResult(framesDisplayed > 0);
            mediaPlayer.EncounteredError += (_, _) => frameReady.TrySetResult(false);

            if (!mediaPlayer.Play())
            {
                return null;
            }

            var completed = await Task.WhenAny(frameReady.Task, Task.Delay(8000));
            var gotFrame = completed == frameReady.Task && frameReady.Task.Result;

            // Stop on a worker thread: it must not run on a LibVLC callback thread
            await Task.Run(() => mediaPlayer.Stop());

            if (!gotFrame && framesDisplayed == 0)
            {
                return null;
            }

            var hasContent = HasContent(buffer, width, height, pitch);
            var pixels = new byte[bufferSize];
            System.Runtime.InteropServices.Marshal.Copy(buffer, pixels, 0, bufferSize);
            return (pixels, hasContent);
        }
        finally
        {
            GC.KeepAlive(lockCb);
            GC.KeepAlive(displayCb);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Cheap check on a grid of sampled pixels: false when the frame is (almost) a single color.
    /// </summary>
    private static bool HasContent(IntPtr buffer, uint width, uint height, uint pitch)
    {
        const int grid = 8;
        int min = 255 * 3, max = 0;
        for (int gy = 0; gy < grid; gy++)
        {
            for (int gx = 0; gx < grid; gx++)
            {
                var x = (int)(width * (gx + 0.5) / grid);
                var y = (int)(height * (gy + 0.5) / grid);
                var offset = y * (int)pitch + x * 4;
                var b = System.Runtime.InteropServices.Marshal.ReadByte(buffer, offset);
                var g = System.Runtime.InteropServices.Marshal.ReadByte(buffer, offset + 1);
                var r = System.Runtime.InteropServices.Marshal.ReadByte(buffer, offset + 2);
                var sum = r + g + b;
                if (sum < min) min = sum;
                if (sum > max) max = sum;
            }
        }
        return max - min > 30;
    }

    private static void SaveJpeg(byte[] bgraPixels, int width, int height, string path)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        System.Runtime.InteropServices.Marshal.Copy(bgraPixels, 0, bitmap.GetPixels(), bgraPixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var stream = File.Create(path);
        data.SaveTo(stream);
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
