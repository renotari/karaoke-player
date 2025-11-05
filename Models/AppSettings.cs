using System;
using System.ComponentModel.DataAnnotations;

namespace KaraokePlayer.Models;

/// <summary>
/// Application settings and configuration
/// </summary>
public class AppSettings
{
    [Key]
    public string Id { get; set; } = "default";

    [Required]
    public string MediaDirectory { get; set; } = string.Empty;

    [Required]
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Single;

    /// <summary>
    /// Volume level (0.0 to 1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public double Volume { get; set; } = 0.8;

    public bool AudioBoostEnabled { get; set; }

    public string AudioOutputDevice { get; set; } = "default";

    public bool CrossfadeEnabled { get; set; }

    /// <summary>
    /// Crossfade duration in seconds (1-20)
    /// </summary>
    [Range(1, 20)]
    public int CrossfadeDuration { get; set; } = 3;

    public bool AutoPlayEnabled { get; set; } = true;

    public bool ShuffleMode { get; set; }

    public string VisualizationStyle { get; set; } = "bars";

    public string Theme { get; set; } = "dark";

    public int FontSize { get; set; } = 14;

    /// <summary>
    /// Preload buffer size in MB
    /// </summary>
    public int PreloadBufferSize { get; set; } = 50;

    /// <summary>
    /// Cache size in MB
    /// </summary>
    public int CacheSize { get; set; } = 500;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

public enum DisplayMode
{
    Single,
    Dual
}
