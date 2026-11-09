using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// Downloads from YouTube: a finished video is saved in the "YouTube" subfolder
/// of the media folder, added to the library and to the end of the queue.
/// One download at a time; they go on when the YouTube window is closed.
/// </summary>
public partial class MainWindowViewModel
{
    private readonly SemaphoreSlim _downloadSlot = new(1, 1);

    /// <summary>Set by the App (null when YouTube downloads are not available).</summary>
    public IYouTubeService? YouTubeService { get; set; }

    /// <summary>Media folder and maximum video height, from the settings (set by the App).</summary>
    public string MediaDirectory { get; set; } = string.Empty;
    public int YouTubeMaxHeight { get; set; } = 720;

    public ObservableCollection<YouTubeDownloadViewModel> Downloads { get; } = new();

    /// <summary>Status bar summary of the downloads in progress (empty when none).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDownloadStatus))]
    private string _downloadStatus = string.Empty;

    public bool HasDownloadStatus => !string.IsNullOrEmpty(DownloadStatus);

    /// <summary>Opens the YouTube window, searching for what is typed in the catalog search.</summary>
    [RelayCommand]
    public void OpenYouTube()
    {
        ReactiveUI.MessageBus.Current.SendMessage(new OpenYouTubeMessage(SearchQuery));
    }

    public async Task DownloadFromYouTubeAsync(YouTubeVideo video)
    {
        if (YouTubeService == null)
            return;

        if (string.IsNullOrWhiteSpace(MediaDirectory) || !Directory.Exists(MediaDirectory))
        {
            _notificationService?.ShowError("No media folder",
                "Choose the media folder first (File > Open Media Directory): downloaded videos are saved there.");
            return;
        }

        if (Downloads.Any(d => d.Video.Id == video.Id && d.IsActive))
            return;

        var folder = Path.Combine(MediaDirectory, "YouTube");
        var existing = YouTubeService.FindExistingDownload(folder, video.Id);
        if (existing != null)
        {
            await EnqueueDownloadedFileAsync(existing, alreadyDownloaded: true);
            return;
        }

        var item = new YouTubeDownloadViewModel(video);
        item.PropertyChanged += (_, _) => UpdateDownloadStatus();
        Downloads.Insert(0, item);
        UpdateDownloadStatus();

        var hasSlot = false;
        try
        {
            await _downloadSlot.WaitAsync(item.Token);
            hasSlot = true;

            item.StatusText = "Preparing...";
            await YouTubeService.EnsureToolsAsync(update: false,
                new Progress<string>(text => item.StatusText = text), item.Token);

            var path = await YouTubeService.DownloadAsync(video, folder, YouTubeMaxHeight,
                new Progress<YouTubeDownloadProgress>(item.Report), item.Token);

            item.Finish(YouTubeDownloadState.Completed, "Downloaded");
            await EnqueueDownloadedFileAsync(path, alreadyDownloaded: false);
        }
        catch (OperationCanceledException)
        {
            item.Finish(YouTubeDownloadState.Cancelled, "Cancelled");
        }
        catch (Exception ex)
        {
            item.Finish(YouTubeDownloadState.Failed, $"Failed: {ex.Message}");
            App.Current?.LoggingService?.LogError($"YouTube download failed: {video.Url}", ex);
            _notificationService?.ShowError("Download failed", $"{video.Title}: {ex.Message}", 10000);
        }
        finally
        {
            if (hasSlot)
                _downloadSlot.Release();
            UpdateDownloadStatus();
        }
    }

    private async Task EnqueueDownloadedFileAsync(string path, bool alreadyDownloaded)
    {
        var file = _mediaLibraryManager == null ? null : await _mediaLibraryManager.AddFileAsync(path);
        if (file == null)
        {
            _notificationService?.ShowError("Download", $"Could not add '{Path.GetFileName(path)}' to the library.");
            return;
        }

        await AddToPlaylist(file, "end");
        _notificationService?.ShowSuccess(
            alreadyDownloaded ? "Already downloaded" : "Download complete",
            $"'{file.DisplayName}' added to the end of the queue.");
    }

    /// <summary>Stops the downloads (on exit: yt-dlp would keep running otherwise).</summary>
    public void CancelAllDownloads()
    {
        foreach (var item in Downloads.Where(d => d.IsActive))
            item.CancelCommand.Execute(null);
    }

    public void ClearFinishedDownloads()
    {
        foreach (var item in Downloads.Where(d => d.IsFinished).ToList())
            Downloads.Remove(item);
    }

    private void UpdateDownloadStatus()
    {
        var active = Downloads.Where(d => d.IsActive).ToList();
        var current = active.FirstOrDefault(d => d.State == YouTubeDownloadState.Downloading) ?? active.FirstOrDefault();
        DownloadStatus = current == null
            ? string.Empty
            : $"⬇ {current.Title} - {current.StatusText}" + (active.Count > 1 ? $" (+{active.Count - 1} waiting)" : string.Empty);
    }
}

/// <summary>Request to open the YouTube window, with the text to search.</summary>
public record OpenYouTubeMessage(string? Query);
