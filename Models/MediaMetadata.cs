using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaraokePlayer.Models;

/// <summary>
/// Metadata extracted from media files
/// </summary>
public class MediaMetadata
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string MediaFileId { get; set; } = string.Empty;

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public double Duration { get; set; }

    public string Artist { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Album { get; set; }

    /// <summary>
    /// Video resolution width (null for audio files)
    /// </summary>
    public int? ResolutionWidth { get; set; }

    /// <summary>
    /// Video resolution height (null for audio files)
    /// </summary>
    public int? ResolutionHeight { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Whether the video file has subtitle tracks (null for audio files)
    /// </summary>
    public bool? HasSubtitles { get; set; }

    // Navigation property
    [ForeignKey(nameof(MediaFileId))]
    public MediaFile? MediaFile { get; set; }
}
