using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KaraokePlayer.Services;

/// <summary>A video found on YouTube (search result or pasted link).</summary>
public record YouTubeVideo(string Id, string Title, string Channel, double? DurationSeconds)
{
    public string Url => $"https://www.youtube.com/watch?v={Id}";
    public string ThumbnailUrl => $"https://i.ytimg.com/vi/{Id}/mqdefault.jpg";
}

/// <summary>Download progress: Percent is null while yt-dlp is not downloading (e.g. merging).</summary>
public record YouTubeDownloadProgress(double? Percent, string Stage);

/// <summary>Where the external programs were found (null = missing).</summary>
public record YouTubeToolsStatus(string? YtDlp, string? Ffmpeg, string? Deno);

/// <summary>
/// Searches and downloads YouTube videos through yt-dlp (external program).
/// yt-dlp needs ffmpeg to merge the separate video/audio streams YouTube serves
/// above 360p, and a JavaScript runtime (deno) to read most YouTube formats.
/// The programs are looked up in the app data Tools folder (downloaded and
/// updated by the app, writable without admin rights), then next to the
/// executable (tools\), then in PATH.
/// </summary>
public interface IYouTubeService
{
    YouTubeToolsStatus GetToolsStatus();

    /// <summary>Downloads the missing tools (all of them when <paramref name="update"/> is true).</summary>
    Task EnsureToolsAsync(bool update, IProgress<string>? status, CancellationToken cancellationToken = default);

    /// <summary>Searches YouTube, or reads the video of a pasted link.</summary>
    Task<List<YouTubeVideo>> SearchAsync(string queryOrUrl, int maxResults, CancellationToken cancellationToken = default);

    /// <summary>Downloads the video into <paramref name="destinationDirectory"/> and returns the file path.</summary>
    Task<string> DownloadAsync(YouTubeVideo video, string destinationDirectory, int maxHeight,
        IProgress<YouTubeDownloadProgress>? progress, CancellationToken cancellationToken = default);

    /// <summary>The file already downloaded for this video in the folder, if any.</summary>
    string? FindExistingDownload(string directory, string videoId);
}

public class YouTubeService : IYouTubeService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly ILoggingService? _logging;

    public YouTubeService(ILoggingService? logging = null)
    {
        _logging = logging;
    }

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static string Exe(string name) => IsWindows ? name + ".exe" : name;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KaraokePlayer/1.0");
        return client;
    }

    // ----- Tools ---------------------------------------------------------------

    public YouTubeToolsStatus GetToolsStatus() =>
        new(FindTool("yt-dlp"), FindTool("ffmpeg"), FindTool("deno"));

    private static string? FindTool(string name)
    {
        var file = Exe(name);
        var candidates = new List<string>
        {
            Path.Combine(AppPaths.Tools, file),
            Path.Combine(AppContext.BaseDirectory, "tools", file),
        };
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir.Trim('"'), file)));

        return candidates.FirstOrDefault(File.Exists);
    }

    private const string YtDlpBase = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/";
    private const string FfmpegWindowsZip = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
    private const string DenoWindowsZip = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

    private readonly SemaphoreSlim _toolsLock = new(1, 1);

    public async Task EnsureToolsAsync(bool update, IProgress<string>? status, CancellationToken cancellationToken = default)
    {
        // A search and a download may ask at the same time: download the tools once
        await _toolsLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureToolsLockedAsync(update, status, cancellationToken);
        }
        finally
        {
            _toolsLock.Release();
        }
    }

    private async Task EnsureToolsLockedAsync(bool update, IProgress<string>? status, CancellationToken cancellationToken)
    {
        var tools = GetToolsStatus();

        if (update || tools.YtDlp == null)
        {
            status?.Report("Downloading yt-dlp...");
            var asset = IsWindows ? "yt-dlp.exe"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "yt-dlp_macos" : "yt-dlp_linux";
            var target = Path.Combine(AppPaths.Tools, Exe("yt-dlp"));
            await DownloadFileAsync(YtDlpBase + asset, target, status, "yt-dlp", cancellationToken);
            MakeExecutable(target);
        }

        // ffmpeg and deno: downloaded on Windows only (on Linux install them with the package manager)
        if (!IsWindows)
            return;

        if (update || tools.Ffmpeg == null)
        {
            status?.Report("Downloading ffmpeg...");
            await DownloadAndExtractAsync(FfmpegWindowsZip, new[] { "ffmpeg.exe", "ffprobe.exe" }, status, "ffmpeg", cancellationToken);
        }

        if (update || tools.Deno == null)
        {
            status?.Report("Downloading deno...");
            await DownloadAndExtractAsync(DenoWindowsZip, new[] { "deno.exe" }, status, "deno", cancellationToken);
        }

        if (update || tools.YtDlp == null || tools.Ffmpeg == null || tools.Deno == null)
            status?.Report("Tools ready");
    }

    private async Task DownloadFileAsync(string url, string target, IProgress<string>? status, string label, CancellationToken ct)
    {
        _logging?.LogInformation($"Downloading {url}");
        var temp = target + ".download";
        using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(temp);

            var buffer = new byte[81920];
            long done = 0;
            int lastPercent = -1;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0)
                {
                    var percent = (int)(done * 100 / total.Value);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        status?.Report($"Downloading {label}... {percent}%");
                    }
                }
            }
        }
        File.Move(temp, target, overwrite: true);
    }

    private async Task DownloadAndExtractAsync(string url, string[] files, IProgress<string>? status, string label, CancellationToken ct)
    {
        var zipPath = Path.Combine(AppPaths.Tools, label + ".zip");
        try
        {
            await DownloadFileAsync(url, zipPath, status, label, ct);
            status?.Report($"Extracting {label}...");
            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(zipPath);
                foreach (var name in files)
                {
                    var entry = zip.Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException($"{name} not found in {label} archive");
                    entry.ExtractToFile(Path.Combine(AppPaths.Tools, name), overwrite: true);
                }
            }, ct);
        }
        finally
        {
            try { File.Delete(zipPath); } catch { /* best effort */ }
        }
    }

    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    // ----- Search ----------------------------------------------------------------

    private static readonly Regex YouTubeUrlPattern = new(
        @"^(https?://)?((www|m|music)\.)?(youtube\.com/(watch\?.*v=|shorts/|live/|embed/)|youtu\.be/)[A-Za-z0-9_-]{11}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsYouTubeUrl(string text) => YouTubeUrlPattern.IsMatch(text.Trim());

    public async Task<List<YouTubeVideo>> SearchAsync(string queryOrUrl, int maxResults, CancellationToken cancellationToken = default)
    {
        var text = queryOrUrl.Trim();
        if (text.Length == 0)
            return new List<YouTubeVideo>();

        var args = new List<string> { "--ignore-config", "--no-warnings", "-J" };
        if (IsYouTubeUrl(text))
            args.AddRange(new[] { "--no-playlist", "--skip-download", text });
        else
            args.AddRange(new[] { "--flat-playlist", $"ytsearch{maxResults}:{text}" });

        var result = await RunYtDlpAsync(args, null, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(ErrorMessage(result, "YouTube search failed"));

        return ParseSearchJson(result.Output);
    }

    /// <summary>Reads yt-dlp's -J output: a search (entries) or a single video.</summary>
    public static List<YouTubeVideo> ParseSearchJson(string json)
    {
        var videos = new List<YouTubeVideo>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        IEnumerable<JsonElement> items = root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array
            ? entries.EnumerateArray()
            : new[] { root };

        foreach (var item in items)
        {
            var id = GetString(item, "id");
            if (id == null || id.Length != 11)
                continue; // channels and playlists in search results

            var title = GetString(item, "title") ?? id;
            var channel = GetString(item, "channel") ?? GetString(item, "uploader") ?? string.Empty;
            double? duration = item.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                ? d.GetDouble()
                : null;
            videos.Add(new YouTubeVideo(id, title, channel, duration));
        }
        return videos;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // ----- Download --------------------------------------------------------------

    /// <summary>
    /// yt-dlp format selection: MP4 up to <paramref name="maxHeight"/>, preferring H.264
    /// (decoded everywhere, also in hardware). Without ffmpeg only single-file
    /// formats can be used (YouTube serves those up to 360p).
    /// </summary>
    public static string BuildFormat(int maxHeight, bool hasFfmpeg)
    {
        var h = $"[height<={maxHeight}]";
        return hasFfmpeg
            ? $"bv*{h}[ext=mp4][vcodec^=avc1]+ba[ext=m4a]/bv*{h}[ext=mp4]+ba[ext=m4a]/b{h}[ext=mp4]/bv*{h}+ba/b{h}/b"
            : $"b{h}[ext=mp4]/b[ext=mp4]/b{h}/b";
    }

    public string? FindExistingDownload(string directory, string videoId)
    {
        if (!Directory.Exists(directory))
            return null;

        var marker = $"[{videoId}]";
        return Directory.EnumerateFiles(directory)
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).EndsWith(marker, StringComparison.Ordinal)
                                 && IsMediaExtension(Path.GetExtension(f)));
    }

    private static bool IsMediaExtension(string extension) =>
        extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);

    public async Task<string> DownloadAsync(YouTubeVideo video, string destinationDirectory, int maxHeight,
        IProgress<YouTubeDownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var tools = GetToolsStatus();
        var hasFfmpeg = tools.Ffmpeg != null;

        var args = new List<string>
        {
            "--ignore-config", "--no-playlist", "--newline", "--windows-filenames",
            "-f", BuildFormat(maxHeight, hasFfmpeg),
            // The id in the name lets the app recognize a video already downloaded
            "-o", "%(title).150B [%(id)s].%(ext)s",
            // Partial files stay out of the media folder (watched by the library)
            "-P", destinationDirectory,
            "-P", "temp:" + AppPaths.DownloadTemp,
            "--progress-template", "download:[progress] %(progress.downloaded_bytes)s %(progress.total_bytes)s %(progress.total_bytes_estimate)s",
        };
        if (hasFfmpeg)
            args.AddRange(new[] { "--merge-output-format", "mp4", "--ffmpeg-location", tools.Ffmpeg! });
        args.Add(video.Url);

        progress?.Report(new YouTubeDownloadProgress(0, "Starting"));
        var stream = 0;
        double lastPercent = 0;
        ProcessResult result;
        try
        {
            result = await RunYtDlpAsync(args, line =>
        {
            if (TryParseProgress(line, out var percent))
            {
                // Video and audio are downloaded one after the other
                if (percent < lastPercent - 50)
                    stream++;
                lastPercent = percent;
                progress?.Report(new YouTubeDownloadProgress(percent, stream == 0 ? "Downloading" : "Downloading audio"));
            }
            else if (line.StartsWith("[Merger]", StringComparison.Ordinal) || line.StartsWith("[VideoConvertor]", StringComparison.Ordinal))
            {
                progress?.Report(new YouTubeDownloadProgress(null, "Merging video and audio"));
            }
        }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            DeletePartialFiles(video.Id);
            throw;
        }

        if (result.ExitCode != 0)
        {
            DeletePartialFiles(video.Id);
            throw new InvalidOperationException(ErrorMessage(result, "Download failed"));
        }

        return FindExistingDownload(destinationDirectory, video.Id)
            ?? throw new InvalidOperationException("Download finished but the file was not found");
    }

    /// <summary>Removes what a failed or cancelled download left in the temp folder.</summary>
    private static void DeletePartialFiles(string videoId)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.DownloadTemp, $"*[{videoId}]*"))
            {
                try { File.Delete(file); } catch { /* still locked: removed next time */ }
            }
        }
        catch { /* best effort */ }
    }

    /// <summary>Parses a "[progress] downloaded total estimate" line (values may be "NA").</summary>
    public static bool TryParseProgress(string line, out double percent)
    {
        percent = 0;
        if (!line.StartsWith("[progress] ", StringComparison.Ordinal))
            return false;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || !TryNumber(parts[1], out var downloaded))
            return false;

        if (!TryNumber(parts[2], out var total) && !TryNumber(parts[3], out total))
            return false;
        if (total <= 0)
            return false;

        percent = Math.Clamp(downloaded / total * 100, 0, 100);
        return true;
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    // ----- Running yt-dlp ------------------------------------------------------

    private record ProcessResult(int ExitCode, string Output, string Errors);

    private static string ErrorMessage(ProcessResult result, string fallback)
    {
        var lines = result.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var error = lines.LastOrDefault(l => l.StartsWith("ERROR:", StringComparison.Ordinal)) ?? lines.LastOrDefault();
        return string.IsNullOrWhiteSpace(error) ? fallback : error.Replace("ERROR: ", string.Empty);
    }

    private async Task<ProcessResult> RunYtDlpAsync(List<string> args, Action<string>? onOutputLine, CancellationToken ct)
    {
        var tools = GetToolsStatus();
        var ytDlp = tools.YtDlp ?? throw new InvalidOperationException("yt-dlp is not installed");

        var info = new ProcessStartInfo(ytDlp)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // Non-ASCII titles: make yt-dlp (Python) write UTF-8 on Windows too
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["PYTHONIOENCODING"] = "utf-8";

        // yt-dlp needs a JavaScript runtime for YouTube (deno is its default)
        if (tools.Deno != null)
        {
            info.ArgumentList.Add("--js-runtimes");
            info.ArgumentList.Add("deno:" + tools.Deno);
        }
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        _logging?.LogInformation($"yt-dlp {string.Join(' ', info.ArgumentList)}");

        using var process = new Process { StartInfo = info };
        var output = new StringBuilder();
        var errors = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            if (onOutputLine != null) onOutputLine(e.Data);
            else output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) lock (errors) errors.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Killed right away on cancel (also when the app is closing)
        using (ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
        }))
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        ct.ThrowIfCancellationRequested();

        // Flush the asynchronous readers
        process.WaitForExit();

        if (process.ExitCode != 0)
            _logging?.LogWarning($"yt-dlp exited with {process.ExitCode}: {errors}");

        return new ProcessResult(process.ExitCode, output.ToString(), errors.ToString());
    }
}
