using System;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for generating thumbnails from media files
/// </summary>
public interface IThumbnailGenerator
{
    /// <summary>
    /// Event raised when thumbnail generation completes for a file
    /// </summary>
    event EventHandler<ThumbnailGeneratedEventArgs>? ThumbnailGenerated;

    /// <summary>
    /// Event raised when thumbnail generation fails for a file
    /// </summary>
    event EventHandler<ThumbnailGenerationFailedEventArgs>? ThumbnailGenerationFailed;

    /// <summary>
    /// Generates a thumbnail from a video file by capturing a frame at 10% duration
    /// </summary>
    /// <param name="mediaFile">The video file to generate thumbnail from</param>
    /// <returns>Path to the generated thumbnail file</returns>
    Task<string> GenerateVideoThumbnailAsync(MediaFile mediaFile);

    /// <summary>
    /// Extracts embedded artwork from an MP3 file
    /// </summary>
    /// <param name="mediaFile">The audio file to extract artwork from</param>
    /// <returns>Path to the extracted artwork file, or null if no artwork found</returns>
    Task<string?> ExtractAudioArtworkAsync(MediaFile mediaFile);

    /// <summary>
    /// Creates a default placeholder thumbnail for files without artwork
    /// </summary>
    /// <param name="mediaType">The type of media (video or audio)</param>
    /// <returns>Path to the placeholder thumbnail file</returns>
    string CreateDefaultThumbnail(Models.MediaType mediaType);

    /// <summary>
    /// Queues a media file for background thumbnail generation
    /// </summary>
    /// <param name="mediaFile">The media file to queue</param>
    void QueueForGeneration(MediaFile mediaFile);

    /// <summary>
    /// Starts the background processing queue
    /// </summary>
    void StartProcessing();

    /// <summary>
    /// Stops the background processing queue
    /// </summary>
    void StopProcessing();

    /// <summary>
    /// Gets whether the background processor is currently running
    /// </summary>
    bool IsProcessing { get; }

    /// <summary>
    /// Gets the number of files waiting in the generation queue
    /// </summary>
    int QueuedCount { get; }

    /// <summary>
    /// Gets the cache directory path where thumbnails are stored
    /// </summary>
    string CacheDirectory { get; }
}

/// <summary>
/// Event args for successful thumbnail generation
/// </summary>
public class ThumbnailGeneratedEventArgs : EventArgs
{
    public MediaFile MediaFile { get; set; } = null!;
    public string ThumbnailPath { get; set; } = string.Empty;
}

/// <summary>
/// Event args for failed thumbnail generation
/// </summary>
public class ThumbnailGenerationFailedEventArgs : EventArgs
{
    public MediaFile MediaFile { get; set; } = null!;
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
