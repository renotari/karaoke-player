using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// "Download from YouTube" window: search (or paste a link), pick a result,
/// download it. Downloads belong to the MainWindowViewModel, so they go on
/// when the window is closed.
/// </summary>
public partial class YouTubeViewModel : ViewModelBase
{
    private readonly IYouTubeService? _youTubeService;
    private readonly MainWindowViewModel? _main;
    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusText = "Type the song (e.g. \"Artist Title karaoke\") or paste a YouTube link.";

    public ObservableCollection<YouTubeResultViewModel> Results { get; } = new();

    public ObservableCollection<YouTubeDownloadViewModel> Downloads =>
        _main?.Downloads ?? new ObservableCollection<YouTubeDownloadViewModel>();

    // Design-time
    public YouTubeViewModel()
    {
        Results.Add(new YouTubeResultViewModel(new YouTubeVideo("dQw4w9WgXcQ", "Artist - Song (Karaoke Version)", "Karaoke Channel", 215)));
    }

    public YouTubeViewModel(IYouTubeService youTubeService, MainWindowViewModel main, string? initialQuery)
    {
        _youTubeService = youTubeService;
        _main = main;
        if (!string.IsNullOrWhiteSpace(initialQuery))
        {
            // Most of the time the user wants the karaoke version of the song
            Query = initialQuery.Trim();
            if (!YouTubeService.IsYouTubeUrl(Query) && !Query.Contains("karaoke", StringComparison.OrdinalIgnoreCase))
                Query += " karaoke";
        }
    }

    private bool CanSearch() => !IsSearching && !string.IsNullOrWhiteSpace(Query) && _youTubeService != null;

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task Search()
    {
        if (_youTubeService == null)
            return;

        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        var token = _searchCancellation.Token;

        IsSearching = true;
        Results.Clear();
        try
        {
            StatusText = "Preparing...";
            await _youTubeService.EnsureToolsAsync(update: false,
                new Progress<string>(text => StatusText = $"{text} (first time only)"), token);

            StatusText = "Searching YouTube...";
            var videos = await _youTubeService.SearchAsync(Query, 15, token);
            foreach (var video in videos)
            {
                var item = new YouTubeResultViewModel(video);
                Results.Add(item);
                _ = item.LoadThumbnailAsync();
            }

            StatusText = videos.Count == 0
                ? "No videos found."
                : $"{videos.Count} videos found. Click Download: the video is saved in the media folder and added to the queue.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task Download(YouTubeResultViewModel? result)
    {
        if (result == null || _main == null)
            return;

        result.IsRequested = true;
        await _main.DownloadFromYouTubeAsync(result.Video);
    }

    /// <summary>Removes the finished, failed and cancelled downloads from the list.</summary>
    [RelayCommand]
    private void ClearFinishedDownloads() => _main?.ClearFinishedDownloads();

    public void CancelSearch() => _searchCancellation?.Cancel();
}

/// <summary>One search result.</summary>
public partial class YouTubeResultViewModel : ObservableObject
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public YouTubeResultViewModel(YouTubeVideo video)
    {
        Video = video;
    }

    public YouTubeVideo Video { get; }
    public string Title => Video.Title;
    public string Channel => Video.Channel;
    public double Duration => Video.DurationSeconds ?? 0;
    public bool HasDuration => Video.DurationSeconds > 0;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    /// <summary>Download clicked (the button shows it was taken into account).</summary>
    [ObservableProperty]
    private bool _isRequested;

    public async Task LoadThumbnailAsync()
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(Video.ThumbnailUrl);
            using var stream = new MemoryStream(bytes);
            Thumbnail = new Bitmap(stream);
        }
        catch
        {
            // No thumbnail: the placeholder stays
        }
    }
}

public enum YouTubeDownloadState { Queued, Downloading, Completed, Failed, Cancelled }

/// <summary>One download (shown in the YouTube window and summarized in the status bar).</summary>
public partial class YouTubeDownloadViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cancellation = new();

    public YouTubeDownloadViewModel(YouTubeVideo video)
    {
        Video = video;
    }

    public YouTubeVideo Video { get; }
    public string Title => Video.Title;
    public CancellationToken Token => _cancellation.Token;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(IsFinished))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private YouTubeDownloadState _state = YouTubeDownloadState.Queued;

    [ObservableProperty]
    private double _percent;

    /// <summary>No percentage available (preparing, merging).</summary>
    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private string _statusText = "Waiting...";

    public bool IsActive => State is YouTubeDownloadState.Queued or YouTubeDownloadState.Downloading;
    public bool IsFinished => !IsActive;

    [RelayCommand(CanExecute = nameof(IsActive))]
    private void Cancel() => _cancellation.Cancel();

    public void Report(YouTubeDownloadProgress progress)
    {
        State = YouTubeDownloadState.Downloading;
        IsIndeterminate = progress.Percent == null;
        if (progress.Percent is { } percent)
        {
            Percent = percent;
            StatusText = $"{progress.Stage} {percent:0}%";
        }
        else
        {
            StatusText = progress.Stage + "...";
        }
    }

    public void Finish(YouTubeDownloadState state, string text)
    {
        State = state;
        IsIndeterminate = false;
        Percent = state == YouTubeDownloadState.Completed ? 100 : Percent;
        StatusText = text;
    }
}
