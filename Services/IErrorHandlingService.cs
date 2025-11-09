using System;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for handling errors and coordinating error recovery
/// </summary>
public interface IErrorHandlingService
{
    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    event EventHandler<MediaErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Handle a corrupted file error
    /// </summary>
    void HandleCorruptedFile(MediaFile mediaFile, string details);

    /// <summary>
    /// Handle a missing file error
    /// </summary>
    void HandleMissingFile(MediaFile mediaFile);

    /// <summary>
    /// Handle a permission denied error
    /// </summary>
    void HandlePermissionDenied(MediaFile mediaFile);

    /// <summary>
    /// Handle a playback failure
    /// </summary>
    void HandlePlaybackFailure(MediaFile mediaFile, string details);

    /// <summary>
    /// Handle a crossfade failure
    /// </summary>
    void HandleCrossfadeFailure(MediaFile currentFile, MediaFile? nextFile, string details);

    /// <summary>
    /// Handle metadata extraction failure
    /// </summary>
    void HandleMetadataExtractionFailure(MediaFile mediaFile, string details);

    /// <summary>
    /// Handle thumbnail generation failure
    /// </summary>
    void HandleThumbnailGenerationFailure(MediaFile mediaFile, string details);

    /// <summary>
    /// Clear all error states (called on application restart)
    /// </summary>
    void ClearAllErrors();

    /// <summary>
    /// Get error for a specific media file
    /// </summary>
    MediaError? GetError(string mediaFileId);
}

/// <summary>
/// Event args for media errors
/// </summary>
public class MediaErrorEventArgs : EventArgs
{
    public MediaFile MediaFile { get; set; } = null!;
    public MediaError Error { get; set; } = null!;
}
