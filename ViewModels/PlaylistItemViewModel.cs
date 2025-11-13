using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KaraokePlayer.Models;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// ViewModel for a playlist item with error handling
/// </summary>
public partial class PlaylistItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private PlaylistItem _playlistItem;

    [ObservableProperty]
    private MediaFile _mediaFile;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private MediaError? _error;

    [ObservableProperty]
    private bool _isCurrentlyPlaying;

    public PlaylistItemViewModel(PlaylistItem playlistItem, MediaFile mediaFile)
    {
        _playlistItem = playlistItem;
        _mediaFile = mediaFile;
    }

    partial void OnMediaFileChanged(MediaFile value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(DisplayArtist));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(ThumbnailPath));
    }

    partial void OnErrorChanged(MediaError? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(ErrorTooltip));
    }

    public bool HasError => Error != null || !string.IsNullOrEmpty(PlaylistItem.Error);

    public string ErrorMessage => Error?.Message ?? PlaylistItem.Error ?? string.Empty;

    public string ErrorTooltip
    {
        get
        {
            if (Error == null && string.IsNullOrEmpty(PlaylistItem.Error))
                return string.Empty;

            var message = Error?.Message ?? PlaylistItem.Error ?? string.Empty;
            var details = Error?.Details;

            return string.IsNullOrEmpty(details) ? message : $"{message}\n{details}";
        }
    }

    public bool IsDuplicate => PlaylistItem.IsDuplicate;

    public string DisplayTitle => MediaFile.Metadata?.Title ?? MediaFile.Filename;

    public string DisplayArtist => MediaFile.Metadata?.Artist ?? "Unknown Artist";

    public string Duration
    {
        get
        {
            if (MediaFile.Metadata?.Duration > 0)
            {
                var ts = TimeSpan.FromSeconds(MediaFile.Metadata.Duration);
                return ts.ToString(@"mm\:ss");
            }
            return "--:--";
        }
    }

    public string? ThumbnailPath => MediaFile.ThumbnailPath;

    public int Position => PlaylistItem.Position;
}
