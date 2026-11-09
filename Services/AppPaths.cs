using System;
using System.IO;

namespace KaraokePlayer.Services;

/// <summary>
/// Single place for the application's data locations
/// (%APPDATA%\KaraokePlayer on Windows, ~/.config/KaraokePlayer on Linux).
/// The root can be overridden with the KARAOKE_PLAYER_DATA_DIR environment
/// variable: the test runners use it so they never touch the real user data.
/// </summary>
public static class AppPaths
{
    public const string DataDirEnvironmentVariable = "KARAOKE_PLAYER_DATA_DIR";

    public static string Root
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable(DataDirEnvironmentVariable);
            var root = !string.IsNullOrWhiteSpace(overridePath)
                ? overridePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KaraokePlayer");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string DatabaseFile => Path.Combine(Root, "karaoke.db");
    public static string CurrentPlaylistFile => Path.Combine(Root, "current-playlist.json");

    public static string Logs => Sub("Logs");
    public static string Thumbnails => Sub("Thumbnails");
    public static string Cache => Sub("cache");
    public static string Playlists => Sub("Playlists");
    /// <summary>yt-dlp, ffmpeg and deno downloaded by the app (YouTube downloads)</summary>
    public static string Tools => Sub("Tools");
    /// <summary>Partial files of YouTube downloads (moved to the media folder when complete)</summary>
    public static string DownloadTemp => Sub("DownloadTemp");

    private static string Sub(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
