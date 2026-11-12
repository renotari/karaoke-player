using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KaraokePlayer.Models;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Services;

/// <summary>The rows of the Properties dialog of a media file (label, value).</summary>
public static class MediaProperties
{
    /// <summary>
    /// <paramref name="info"/> is the file on disk (null or not existing: the file is
    /// missing, the size falls back to the one saved with the metadata).
    /// </summary>
    public static List<(string Label, string Value)> Build(MediaFile file, FileInfo? info)
    {
        var rows = new List<(string, string)>();
        var metadata = file.MetadataLoaded ? file.Metadata : null;

        rows.Add(("Title", file.DisplayTitle));
        if (!string.IsNullOrWhiteSpace(file.DisplayArtist))
            rows.Add(("Artist", file.DisplayArtist));
        if (!string.IsNullOrWhiteSpace(metadata?.Album))
            rows.Add(("Album", metadata!.Album!));

        rows.Add(("Duration", metadata?.Duration > 0
            ? SecondsToTimeConverter.Format(metadata.Duration)
            : "Unknown"));
        rows.Add(("Type", $"{file.Type} ({file.Format})"));
        if (metadata?.ResolutionWidth > 0 && metadata.ResolutionHeight > 0)
            rows.Add(("Resolution", $"{metadata.ResolutionWidth} × {metadata.ResolutionHeight}"));
        if (metadata?.HasSubtitles is bool subtitles)
            rows.Add(("Subtitles", subtitles ? "Yes" : "No"));

        var exists = info?.Exists == true;
        var size = exists ? info!.Length : metadata?.FileSize ?? 0;
        if (size > 0)
            rows.Add(("Size", FormatSize(size)));

        rows.Add(("File name", file.Filename));
        rows.Add(("Folder", Path.GetDirectoryName(file.FilePath) ?? string.Empty));
        if (exists)
            rows.Add(("Modified", FormatDate(info!.LastWriteTime)));
        rows.Add(("Added to library", FormatDate(file.CreatedAt.ToLocalTime())));

        rows.Add(("Artist/title from", !file.MetadataLoaded
            ? "Not read yet"
            : !string.IsNullOrWhiteSpace(file.Metadata?.Title) ? "File tags" : "File name"));
        if (!exists)
            rows.Add(("Status", "File not found"));
        else if (!string.IsNullOrWhiteSpace(file.ErrorMessage))
            rows.Add(("Status", file.ErrorMessage!));

        return rows;
    }

    /// <summary>"512 bytes", "1.5 KB", "23.4 MB", "1.20 GB".</summary>
    public static string FormatSize(long bytes)
    {
        const double kb = 1024, mb = kb * 1024, gb = mb * 1024;
        var c = CultureInfo.InvariantCulture;
        return bytes switch
        {
            < 1024 => $"{bytes} bytes",
            < 1024 * 1024 => (bytes / kb).ToString("0.0", c) + " KB",
            < 1024L * 1024 * 1024 => (bytes / mb).ToString("0.0", c) + " MB",
            _ => (bytes / gb).ToString("0.00", c) + " GB"
        };
    }

    private static string FormatDate(DateTime date) => date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
