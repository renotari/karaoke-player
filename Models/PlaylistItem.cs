using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaraokePlayer.Models;

/// <summary>
/// Represents an item in the current playlist queue
/// </summary>
public class PlaylistItem
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string MediaFileId { get; set; } = string.Empty;

    /// <summary>
    /// Position in the playlist (0-based index)
    /// </summary>
    public int Position { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this song is a duplicate in the current playlist
    /// </summary>
    public bool IsDuplicate { get; set; }

    /// <summary>
    /// Error message if the playlist item has issues
    /// </summary>
    public string? Error { get; set; }

    // Navigation property
    [ForeignKey(nameof(MediaFileId))]
    public MediaFile? MediaFile { get; set; }
}
