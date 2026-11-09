using System.IO;
using System.Text.RegularExpressions;

namespace KaraokePlayer.Services;

/// <summary>
/// Extracts artist and title from a media file name.
/// Patterns: "Artist - Title", "Artist-Title", "Title (Artist)", "Artist_Title"
/// </summary>
public static class FilenameParser
{
    private static readonly Regex YouTubeIdSuffix = new(@"\s*\[[A-Za-z0-9_-]{11}\]$", RegexOptions.Compiled);

    public static (string Artist, string Title) Parse(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return (string.Empty, string.Empty);
        }

        // Files downloaded from YouTube end with the video id: "Title [dQw4w9WgXcQ]"
        filename = YouTubeIdSuffix.Replace(filename, string.Empty);

        // Pattern 1: "Artist - Title" (with space around dash)
        var match = Regex.Match(filename, @"^(.+?)\s+-\s+(.+)$");
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
        }

        // Pattern 2: "Artist-Title" (no space around dash)
        match = Regex.Match(filename, @"^(.+?)-(.+)$");
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
        }

        // Pattern 3: "Title (Artist)"
        match = Regex.Match(filename, @"^(.+?)\s*\((.+?)\)$");
        if (match.Success)
        {
            return (match.Groups[2].Value.Trim(), match.Groups[1].Value.Trim());
        }

        // Pattern 4: "Artist_Title" (underscore separator)
        match = Regex.Match(filename, @"^(.+?)_(.+)$");
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
        }

        // No pattern matched - use entire filename as title, empty artist
        return (string.Empty, filename.Trim());
    }

    /// <summary>Parses a file name that still has its extension.</summary>
    public static (string Artist, string Title) ParseWithExtension(string filename)
        => Parse(Path.GetFileNameWithoutExtension(filename));
}
