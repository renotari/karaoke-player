using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaraokePlayer.Models;

/// <summary>
/// Represents a media file in the library (video or audio)
/// </summary>
public class MediaFile : INotifyPropertyChanged
{
    /// <summary>
    /// Raised by <see cref="NotifyChanged"/> when background work (metadata,
    /// thumbnail) updates this instance, so bound UI refreshes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Refreshes every binding on this instance.</summary>
    public void NotifyChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

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

    // Display helpers (not stored): use metadata when available,
    // otherwise fall back to what can be parsed from the file name.

    [NotMapped]
    public string DisplayArtist =>
        !string.IsNullOrWhiteSpace(Metadata?.Artist)
            ? Metadata!.Artist
            : KaraokePlayer.Services.FilenameParser.ParseWithExtension(Filename).Artist;

    [NotMapped]
    public string DisplayTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Metadata?.Title))
                return Metadata!.Title;
            var parsed = KaraokePlayer.Services.FilenameParser.ParseWithExtension(Filename).Title;
            return string.IsNullOrWhiteSpace(parsed) ? Filename : parsed;
        }
    }

    /// <summary>"Artist - Title", or just the title when the artist is unknown.</summary>
    [NotMapped]
    public string DisplayName =>
        string.IsNullOrWhiteSpace(DisplayArtist) ? DisplayTitle : $"{DisplayArtist} - {DisplayTitle}";
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
