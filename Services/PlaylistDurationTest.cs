using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>Playlist total/remaining time and the rows of the Properties dialog.</summary>
public static class PlaylistDurationTest
{
    private static void Check(bool condition, string what)
    {
        if (condition) Console.WriteLine($"  ✓ {what}");
        else { TestFailures.Fail($"  ✗ {what}"); throw new Exception(what); }
    }

    public static Task RunTests()
    {
        // Song playing (tags say 200 s, the player 210 s) at 60 s, then 180 s, unknown, 3600 s
        var durations = new double?[] { 200, 180, null, 3600 };
        var s = PlaylistDuration.Compute(durations, firstIsPlaying: true, position: 60, playerDuration: 210);
        Check(s.Total == 210 + 180 + 3600, "total uses the player length for the song playing");
        Check(s.Remaining == 150 + 180 + 3600, "remaining: rest of the song playing plus the queue");
        Check(s.UnknownCount == 1, "unknown lengths are counted apart");
        Check(s.Format(true, 4) == "Total 1:06:30 · Remaining 1:05:30 · 1 without length",
            $"summary text ({s.Format(true, 4)})");

        var idle = PlaylistDuration.Compute(durations, firstIsPlaying: false, position: 60, playerDuration: 210);
        Check(idle.Total == 200 + 180 + 3600 && idle.Remaining == idle.Total, "idle: the first song is still all to play");
        Check(idle.Format(false, 4) == "Total 1:06:20 · 1 without length", "idle: no remaining time shown");

        var past = PlaylistDuration.Compute(new double?[] { 100 }, true, 130, 0);
        Check(past.Remaining == 0, "position past the tag length: remaining never negative");
        Check(PlaylistDuration.Compute(Array.Empty<double?>(), false, 0, 0).Format(false, 0) == string.Empty,
            "empty playlist: no summary");

        // Properties of a file that is not on disk
        var file = new MediaFile
        {
            FilePath = Path.Combine(Path.GetTempPath(), "nowhere", "Queen - Bohemian Rhapsody.mp4"),
            Filename = "Queen - Bohemian Rhapsody.mp4",
            Type = MediaType.Video,
            Format = MediaFormat.MP4,
            MetadataLoaded = true,
            Metadata = new MediaMetadata { Duration = 354, ResolutionWidth = 1920, ResolutionHeight = 1080, FileSize = 3 * 1024 * 1024 }
        };
        var rows = MediaProperties.Build(file, new FileInfo(file.FilePath)).ToDictionary(r => r.Label, r => r.Value);
        Check(rows["Title"] == "Bohemian Rhapsody" && rows["Artist"] == "Queen", "title/artist from the file name");
        Check(rows["Artist/title from"] == "File name", "says where artist/title come from");
        Check(rows["Duration"] == "5:54" && rows["Resolution"] == "1920 × 1080", "duration and resolution");
        Check(rows["Size"] == "3.0 MB", "size from the metadata when the file is missing");
        Check(rows["Status"] == "File not found" && !rows.ContainsKey("Modified"), "missing file reported");
        Check(MediaProperties.FormatSize(512) == "512 bytes" && MediaProperties.FormatSize(1536) == "1.5 KB"
              && MediaProperties.FormatSize(5L * 1024 * 1024 * 1024) == "5.00 GB", "size units");

        var notRead = MediaProperties.Build(new MediaFile { FilePath = file.FilePath, Filename = file.Filename }, null)
            .ToDictionary(r => r.Label, r => r.Value);
        Check(notRead["Duration"] == "Unknown" && notRead["Artist/title from"] == "Not read yet", "metadata not read yet");

        return Task.CompletedTask;
    }
}
