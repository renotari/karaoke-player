using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Services;

/// <summary>YouTube download queue: one at a time, in order, Clear queue (fake yt-dlp).</summary>
public static class DownloadQueueTest
{
    private static void Check(bool condition, string what)
    {
        if (condition) Console.WriteLine($"  ✓ {what}");
        else { TestFailures.Fail($"  ✗ {what}"); throw new Exception(what); }
    }

    /// <summary>Each download waits until the test releases it.</summary>
    private sealed class FakeYouTube : IYouTubeService
    {
        public readonly Dictionary<string, TaskCompletionSource> Gates = new();
        public readonly List<string> Started = new();

        public YouTubeToolsStatus GetToolsStatus() => new("yt-dlp", "ffmpeg", "deno");
        public Task EnsureToolsAsync(bool update, IProgress<string>? status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<YouTubeVideo>> SearchAsync(string queryOrUrl, int maxResults, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<YouTubeVideo>());
        public string? FindExistingDownload(string directory, string videoId) => null;

        public async Task<string> DownloadAsync(YouTubeVideo video, string destinationDirectory, int maxHeight,
            IProgress<YouTubeDownloadProgress>? progress, CancellationToken cancellationToken = default)
        {
            lock (Started) Started.Add(video.Id);
            await Gates[video.Id].Task.WaitAsync(cancellationToken);
            return Path.Combine(destinationDirectory, video.Id + ".mp4");
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10);
    }

    public static async Task RunTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dlqueue-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fake = new FakeYouTube();
            var vm = new MainWindowViewModel { YouTubeService = fake, MediaDirectory = dir };
            var videos = new[] { "a", "b", "c", "d" }.Select(id => new YouTubeVideo(id, "Song " + id, "Channel", 100)).ToList();
            foreach (var v in videos)
                fake.Gates[v.Id] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = videos.Select(v => vm.DownloadFromYouTubeAsync(v)).ToList();
            await Until(() => fake.Started.Count == 1);

            Check(vm.Downloads.Select(d => d.Video.Id).SequenceEqual(new[] { "a", "b", "c", "d" }),
                "downloads listed in the order requested");
            Check(fake.Started.SequenceEqual(new[] { "a" }) && vm.Downloads[0].IsDownloading,
                "one download at a time");
            Check(vm.Downloads[1].StatusText == "Waiting in queue (1st)" && vm.Downloads[3].StatusText == "Waiting in queue (3rd)"
                  && !vm.Downloads[1].IsDownloading, "the others wait, with their place in the queue");
            Check(vm.HasQueuedDownloads && vm.DownloadQueueSummary == "1 downloading, 3 waiting", "queue summary");

            // The first one ends: the next in order starts
            fake.Gates["a"].SetResult();
            await tasks[0];
            await Until(() => fake.Started.Count == 2);
            Check(fake.Started.SequenceEqual(new[] { "a", "b" }), "next download starts in order");
            Check(vm.Downloads.First(d => d.Video.Id == "c").StatusText == "Waiting in queue (1st)", "queue places move up");

            // Clear queue: the waiting ones leave, the one in progress goes on
            vm.ClearDownloadQueue();
            await Task.WhenAll(tasks[2], tasks[3]);
            Check(vm.Downloads.Select(d => d.Video.Id).SequenceEqual(new[] { "a", "b" }), "queued downloads removed from the list");
            Check(vm.Downloads[1].IsDownloading && !vm.HasQueuedDownloads, "the download in progress is not stopped");

            fake.Gates["b"].SetResult();
            await tasks[1];
            Check(fake.Started.Count == 2 && vm.DownloadQueueSummary == string.Empty, "cleared downloads never started");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
