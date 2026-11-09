using System;
using System.IO;
using System.Threading.Tasks;

namespace KaraokePlayer.Services;

/// <summary>
/// Tests of the YouTube download parts that do not need yt-dlp or the network:
/// reading yt-dlp output, link recognition, format selection, file names.
/// </summary>
public static class YouTubeServiceTest
{
    private static void Check(bool condition, string what)
    {
        Console.WriteLine($"  {(condition ? "✓" : "✗")} {what}");
        if (!condition)
            throw new Exception(what);
    }

    public static Task RunTests()
    {
        Console.WriteLine("=== YouTube Service Tests ===\n");

        // Search output (yt-dlp --flat-playlist -J "ytsearchN:...")
        var search = """
        {"_type": "playlist", "id": "volare karaoke", "entries": [
          {"_type": "url", "ie_key": "Youtube", "id": "AbCdEfGhIjK", "url": "https://www.youtube.com/watch?v=AbCdEfGhIjK",
           "title": "Volare - Karaoke (Modugno) è", "duration": 201.0, "channel": "Karaoke TV", "uploader": "x"},
          {"_type": "url", "id": "UCxxxxxxxxxxxxxxxxxxxxxx", "title": "A channel"},
          {"_type": "url", "id": "ZyXwVuTsRqP", "title": "No duration", "uploader": "Someone"}
        ]}
        """;
        var videos = YouTubeService.ParseSearchJson(search);
        Check(videos.Count == 2, "search: channels skipped, 2 videos");
        Check(videos[0].Id == "AbCdEfGhIjK" && videos[0].Title == "Volare - Karaoke (Modugno) è", "search: id and title (UTF-8)");
        Check(videos[0].Channel == "Karaoke TV" && videos[0].DurationSeconds == 201, "search: channel and duration");
        Check(videos[1].Channel == "Someone" && videos[1].DurationSeconds == null, "search: uploader fallback, missing duration");
        Check(videos[0].ThumbnailUrl == "https://i.ytimg.com/vi/AbCdEfGhIjK/mqdefault.jpg", "search: thumbnail url");

        // Single video (pasted link)
        var single = YouTubeService.ParseSearchJson("""{"id": "dQw4w9WgXcQ", "title": "Song", "duration": 212, "channel": "C"}""");
        Check(single.Count == 1 && single[0].Url == "https://www.youtube.com/watch?v=dQw4w9WgXcQ", "single video from a link");

        // Links
        Check(YouTubeService.IsYouTubeUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), "link: watch");
        Check(YouTubeService.IsYouTubeUrl("https://www.youtube.com/watch?list=PL1&v=dQw4w9WgXcQ"), "link: watch with playlist");
        Check(YouTubeService.IsYouTubeUrl("youtu.be/dQw4w9WgXcQ?t=10"), "link: short, no scheme");
        Check(YouTubeService.IsYouTubeUrl(" https://m.youtube.com/shorts/dQw4w9WgXcQ "), "link: shorts");
        Check(!YouTubeService.IsYouTubeUrl("volare karaoke"), "not a link: text");
        Check(!YouTubeService.IsYouTubeUrl("https://example.com/watch?v=dQw4w9WgXcQ"), "not a link: other site");

        // Progress lines (--progress-template)
        Check(YouTubeService.TryParseProgress("[progress] 512 1024 NA", out var p) && Math.Abs(p - 50) < 0.01, "progress: total");
        Check(YouTubeService.TryParseProgress("[progress] 250 NA 1000.5", out p) && Math.Abs(p - 24.99) < 0.1, "progress: estimate");
        Check(!YouTubeService.TryParseProgress("[progress] 250 NA NA", out _), "progress: unknown total");
        Check(!YouTubeService.TryParseProgress("[download] Destination: x.mp4", out _), "progress: other line");

        // Format selection
        var withFfmpeg = YouTubeService.BuildFormat(720, hasFfmpeg: true);
        Check(withFfmpeg.StartsWith("bv*[height<=720][ext=mp4][vcodec^=avc1]+ba[ext=m4a]/") && withFfmpeg.EndsWith("/b"), "format: with ffmpeg");
        Check(!YouTubeService.BuildFormat(1080, hasFfmpeg: false).Contains('+'), "format: without ffmpeg, single file only");

        // File names: the id is not part of the title
        var parsed = FilenameParser.ParseWithExtension("Domenico Modugno - Volare (Karaoke) [AbCdEfGhIjK].mp4");
        Check(parsed.Artist == "Domenico Modugno" && parsed.Title == "Volare (Karaoke)", "file name: YouTube id removed");
        parsed = FilenameParser.ParseWithExtension("Artist - Title [Live].mp4");
        Check(parsed.Title == "Title [Live]", "file name: other brackets kept");

        // Already downloaded
        var dir = Path.Combine(Path.GetTempPath(), "yt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var service = new YouTubeService();
            File.WriteAllText(Path.Combine(dir, "Song [AbCdEfGhIjK].mp4"), "x");
            File.WriteAllText(Path.Combine(dir, "Other [ZyXwVuTsRqP].jpg"), "x");
            Check(service.FindExistingDownload(dir, "AbCdEfGhIjK") != null, "existing download found");
            Check(service.FindExistingDownload(dir, "ZyXwVuTsRqP") == null, "non-video file ignored");
            Check(service.FindExistingDownload(Path.Combine(dir, "missing"), "AbCdEfGhIjK") == null, "missing folder");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        Console.WriteLine();
        return Task.CompletedTask;
    }
}
