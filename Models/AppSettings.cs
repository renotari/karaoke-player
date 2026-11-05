using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

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

    // Window layout, restored at startup (stored in settings.json only, not in the database)

    [NotMapped]
    public WindowBounds? MainWindowBounds { get; set; }

    [NotMapped]
    public WindowBounds? PlaybackWindowBounds { get; set; }

    /// <summary>Width of the catalog column in the main window (pixels)</summary>
    [NotMapped]
    public double CatalogColumnWidth { get; set; } = 300;

    /// <summary>Width of the playlist column in the main window (pixels)</summary>
    [NotMapped]
    public double PlaylistColumnWidth { get; set; } = 300;

    /// <summary>
    /// Keyboard shortcuts stored as JSON
    /// </summary>
    public string KeyboardShortcutsJson { get; set; } = string.Empty;

    /// <summary>
    /// Keyboard shortcuts dictionary (not mapped to database)
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> KeyboardShortcuts
    {
        get
        {
            if (string.IsNullOrEmpty(KeyboardShortcutsJson))
            {
                return GetDefaultKeyboardShortcuts();
            }
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(KeyboardShortcutsJson) 
                       ?? GetDefaultKeyboardShortcuts();
            }
            catch
            {
                return GetDefaultKeyboardShortcuts();
            }
        }
        set
        {
            KeyboardShortcutsJson = JsonSerializer.Serialize(value);
        }
    }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Get default keyboard shortcuts
    /// </summary>
    public static Dictionary<string, string> GetDefaultKeyboardShortcuts()
    {
        return new Dictionary<string, string>
        {
            // Playback Controls
            { "PlayPause", "Space" },
            { "Stop", "S" },
            { "Next", "Right" },
            { "Previous", "Left" },
            { "VolumeUp", "Up" },
            { "VolumeDown", "Down" },
            { "Mute", "M" },
            { "ToggleFullscreen", "F11" },
            
            // Playlist Management
            { "AddToPlaylistEnd", "Ctrl+A" },
            { "AddToPlaylistNext", "Ctrl+Shift+A" },
            { "RemoveFromPlaylist", "Delete" },
            { "ClearPlaylist", "Ctrl+L" },
            { "ShufflePlaylist", "Ctrl+S" },
            
            // Navigation
            { "FocusSearch", "Ctrl+F" },
            { "OpenPlaylistComposer", "Ctrl+P" },
            { "OpenSettings", "Ctrl+Comma" },
            { "RefreshLibrary", "Ctrl+R" },
            { "ToggleDisplayMode", "Ctrl+D" },
            { "CloseDialog", "Escape" }
        };
    }
}

public enum DisplayMode
{
    Single,
    Dual
}

/// <summary>Position and size of a window (device-independent size, screen position in pixels).</summary>
public class WindowBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}
