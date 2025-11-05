using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaraokePlayer.Models;

/// <summary>
/// Represents a media file in the library (video or audio)
/// </summary>
public class MediaFile
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public string Filename { get; set; } = string.Empty;

    [Required]
    public MediaType Type { get; set; }

    [Required]
    public MediaFormat Format { get; set; }

    public string? ThumbnailPath { get; set; }

    public bool MetadataLoaded { get; set; }

    public bool ThumbnailLoaded { get; set; }

    public string? ErrorMessage { get; set; }

    // Navigation property
    public MediaMetadata? Metadata { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }
}

public enum MediaType
{
    Video,
    Audio
}

public enum MediaFormat
{
    MP4,
    MKV,
    WEBM,
    MP3
}
