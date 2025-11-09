using System;
using ReactiveUI;
using KaraokePlayer.Models;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// ViewModel for a playlist item with error handling
/// </summary>
public class PlaylistItemViewModel : ViewModelBase
{
    private PlaylistItem _playlistItem;
    private MediaFile _mediaFile;
    private bool _isPlaying;
    private MediaError? _error;

    public PlaylistItemViewModel(PlaylistItem playlistItem, MediaFile mediaFile)
    {
        _playlistItem = playlistItem;
        _mediaFile = mediaFile;
    }

    public PlaylistItem PlaylistItem
    {
        get => _playlistItem;
        set => this.RaiseAndSetIfChanged(ref _playlistItem, value);
    }

    public MediaFile MediaFile
    {
        get => _mediaFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _mediaFile, value);
            this.RaisePropertyChanged(nameof(DisplayTitle));
            this.RaisePropertyChanged(nameof(DisplayArtist));
            this.RaisePropertyChanged(nameof(Duration));
            this.RaisePropertyChanged(nameof(ThumbnailPath));
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
    }

    public MediaError? Error
    {
        get => _error;
        set
        {
            this.RaiseAndSetIfChanged(ref _error, value);
            this.RaisePropertyChanged(nameof(HasError));
            this.RaisePropertyChanged(nameof(ErrorMessage));
            this.RaisePropertyChanged(nameof(ErrorTooltip));
        }
    }

    public bool HasError => _error != null || !string.IsNullOrEmpty(_playlistItem.Error);

    public string ErrorMessage => _error?.Message ?? _playlistItem.Error ?? string.Empty;

    public string ErrorTooltip
    {
        get
        {
            if (_error == null && string.IsNullOrEmpty(_playlistItem.Error))
                return string.Empty;

            var message = _error?.Message ?? _playlistItem.Error ?? string.Empty;
            var details = _error?.Details;

            return string.IsNullOrEmpty(details) ? message : $"{message}\n{details}";
        }
    }

    public bool IsDuplicate => _playlistItem.IsDuplicate;

    public string DisplayTitle => _mediaFile.Metadata?.Title ?? _mediaFile.Filename;

    public string DisplayArtist => _mediaFile.Metadata?.Artist ?? "Unknown Artist";

    public string Duration
    {
        get
        {
            if (_mediaFile.Metadata?.Duration > 0)
            {
                var ts = TimeSpan.FromSeconds(_mediaFile.Metadata.Duration);
                return ts.ToString(@"mm\:ss");
            }
            return "--:--";
        }
    }

    public string? ThumbnailPath => _mediaFile.ThumbnailPath;

    public int Position => _playlistItem.Position;

    private bool _isCurrentlyPlaying;
    public bool IsCurrentlyPlaying
    {
        get => _isCurrentlyPlaying;
        set => this.RaiseAndSetIfChanged(ref _isCurrentlyPlaying, value);
    }
}
