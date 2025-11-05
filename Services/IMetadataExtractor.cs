using System;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for extracting metadata from media files
/// </summary>
public interface IMetadataExtractor
{
    /// <summary>
    /// Event raised when metadata extraction completes for a file
    /// </summary>
    event EventHandler<MetadataExtractedEventArgs>? MetadataExtracted;

    /// <summary>
    /// Event raised when metadata extraction fails for a file
    /// </summary>
    event EventHandler<MetadataExtractionFailedEventArgs>? MetadataExtractionFailed;

    /// <summary>
    /// Extracts metadata from a video file
    /// </summary>
    /// <param name="mediaFile">The media file to extract metadata from</param>
    /// <returns>Task that completes when extraction is done</returns>
    Task<MediaMetadata> ExtractVideoMetadataAsync(MediaFile mediaFile);

    /// <summary>
    /// Extracts metadata from an audio file (MP3)
    /// </summary>
    /// <param name="mediaFile">The media file to extract metadata from</param>
    /// <returns>Task that completes when extraction is done</returns>
    Task<MediaMetadata> ExtractAudioMetadataAsync(MediaFile mediaFile);

    /// <summary>
    /// Parses filename to extract artist and title using multiple patterns
    /// </summary>
    /// <param name="filename">The filename to parse (without extension)</param>
    /// <returns>Tuple containing artist and title</returns>
    (string Artist, string Title) ParseFilename(string filename);

    /// <summary>
    /// Queues a media file for background metadata extraction
    /// </summary>
    /// <param name="mediaFile">The media file to queue</param>
    void QueueForExtraction(MediaFile mediaFile);

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
    /// Gets the number of files waiting in the extraction queue
    /// </summary>
    int QueuedCount { get; }
}

/// <summary>
/// Event args for successful metadata extraction
/// </summary>
public class MetadataExtractedEventArgs : EventArgs
{
    public MediaFile MediaFile { get; set; } = null!;
    public MediaMetadata Metadata { get; set; } = null!;
}

/// <summary>
/// Event args for failed metadata extraction
/// </summary>
public class MetadataExtractionFailedEventArgs : EventArgs
{
    public MediaFile MediaFile { get; set; } = null!;
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
