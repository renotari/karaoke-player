using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>M3U reading and matching: relative paths, duplicate names, safe file names.</summary>
public static class PlaylistFileTest
{
    private static void Check(bool condition, string what)
    {
        if (condition) Console.WriteLine($"  ✓ {what}");
        else { TestFailures.Fail($"  ✗ {what}"); throw new Exception(what); }
    }

    private static MediaFile File(string path) => new()
    {
        Id = Guid.NewGuid().ToString(),
        FilePath = path,
        Filename = Path.GetFileName(path),
        Type = MediaType.Video,
        Format = MediaFormat.MP4
    };

    public static Task RunTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "m3u-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "lists"));
        try
        {
            var a = Path.Combine(dir, "Rock", "song.mp4");
            var b = Path.Combine(dir, "Pop", "song.mp4");
            var unique = Path.Combine(dir, "Pop", "Città è bella.mp4");
            var library = new List<MediaFile> { File(a), File(b), File(unique) };

            var playlist = Path.Combine(dir, "lists", "party.m3u");
            System.IO.File.WriteAllLines(playlist, new[]
            {
                "#EXTM3U", "#EXTINF:200,X - Y",
                "../Rock/song.mp4",                // relative to the playlist folder
                b,                                 // absolute
                "D:\\Old PC\\Città è bella.mp4",   // another PC: unique name
                "D:\\Old PC\\song.mp4",            // another PC: ambiguous name
                "missing.mp4"
            });

            var entries = PlaylistFile.ReadEntries(playlist);
            Check(entries.Count == 5, "comments skipped, 5 entries");
            Check(PlaylistFile.Resolve(entries[0], library)?.FilePath == a, "relative path resolved against the playlist folder");
            Check(PlaylistFile.Resolve(entries[1], library)?.FilePath == b, "absolute path, same name in two folders: the exact one");
            Check(PlaylistFile.Resolve(entries[2], library)?.FilePath == unique, "path from another PC: unique file name (Unicode)");
            Check(PlaylistFile.Resolve(entries[3], library) == null, "ambiguous file name: not guessed");
            Check(PlaylistFile.Resolve(entries[4], library) == null, "missing song");

            Check(PlaylistFile.SafeFileName("../../evil") == "_.._evil", "no folders in a playlist name");
            Check(PlaylistFile.SafeFileName("C:\\x?y*") == "C__x_y_", "characters Windows refuses replaced");
            Check(PlaylistFile.SafeFileName(" .. ") == string.Empty, "nothing usable left");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
        return Task.CompletedTask;
    }
}
