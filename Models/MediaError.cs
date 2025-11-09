using System;

namespace KaraokePlayer.Models;

/// <summary>
/// Represents an error associated with a media file
/// </summary>
public class MediaError
{
    public MediaErrorType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}

/// <summary>
/// Types of errors that can occur with media files
/// </summary>
public enum MediaErrorType
{
    None,
    Corrupted,
    Missing,
    PermissionDenied,
    UnsupportedFormat,
    MetadataExtractionFailed,
    ThumbnailGenerationFailed,
    PlaybackFailed,
    CrossfadeFailed
}
