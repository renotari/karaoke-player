using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// M3U/M3U8 helpers shared by the playlist manager and the Playlist Composer:
/// reading the entries (relative paths resolved against the playlist folder)
/// and matching them with library files.
/// </summary>
public static class PlaylistFile
{
    /// <summary>The song entries of a playlist, as absolute paths.</summary>
    public static List<string> ReadEntries(string playlistPath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(playlistPath)) ?? string.Empty;
        var entries = new List<string>();

        foreach (var raw in File.ReadAllLines(playlistPath, Encoding.UTF8))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            entries.Add(ToAbsolutePath(line, folder));
        }
        return entries;
    }

    public static string ToAbsolutePath(string entry, string playlistFolder)
    {
        if (entry.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(entry, UriKind.Absolute, out var uri))
        {
            return uri.LocalPath;
        }

        // Playlists written on Windows use '\' (and the other way round)
        var path = entry.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (OperatingSystem.IsWindows() || !IsWindowsRooted(entry))
        {
            try
            {
                return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(playlistFolder, path));
            }
            catch (Exception)
            {
                // Invalid characters: keep the text, it simply will not match
            }
        }
        return entry;
    }

    private static bool IsWindowsRooted(string entry) =>
        entry.Length >= 2 && entry[1] == ':' && char.IsLetter(entry[0]);

    /// <summary>
    /// The library file for an entry: same path, or else the only library file
    /// with that file name (playlists from another PC or a moved folder). When
    /// several files share the name, nothing is chosen: a wrong song is worse
    /// than a missing one.
    /// </summary>
    public static MediaFile? Resolve(string entryPath, IReadOnlyCollection<MediaFile> library)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var exact = library.FirstOrDefault(f => string.Equals(f.FilePath, entryPath, comparison));
        if (exact != null)
            return exact;

        var name = Path.GetFileName(entryPath.Replace('\\', '/'));
        var sameName = library
            .Where(f => string.Equals(f.Filename, name, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        return sameName.Count == 1 ? sameName[0] : null;
    }

    /// <summary>
    /// A playlist name usable as a file name inside the Playlists folder
    /// (no folders, no characters Windows refuses). Empty when nothing is left.
    /// </summary>
    public static string SafeFileName(string name)
    {
        var invalid = new HashSet<char>("<>:\"/\\|?*");
        var cleaned = new string(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray());
        return cleaned.Trim().Trim('.').Trim();
    }
}
