using ReactiveUI;
using KaraokePlayer.Models;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// View model wrapper for PlaylistItem with additional UI state
/// </summary>
public class PlaylistItemViewModel : ViewModelBase
{
    private bool _isCurrentlyPlaying;

    public PlaylistItemViewModel(PlaylistItem playlistItem)
    {
        PlaylistItem = playlistItem;
    }

    public PlaylistItem PlaylistItem { get; }

    // Convenience properties for binding
    public string Id => PlaylistItem.Id;
    public MediaFile? MediaFile => PlaylistItem.MediaFile;
    public bool IsDuplicate => PlaylistItem.IsDuplicate;
    public string? Error => PlaylistItem.Error;
    public int Position => PlaylistItem.Position;

    public bool IsCurrentlyPlaying
    {
        get => _isCurrentlyPlaying;
        set => this.RaiseAndSetIfChanged(ref _isCurrentlyPlaying, value);
    }
}
