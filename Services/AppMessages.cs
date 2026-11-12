namespace KaraokePlayer.Services;

// Messages sent through ReactiveUI's MessageBus from the ViewModel to the main window
// (which opens the corresponding windows).

/// <summary>Request to open the Playlist Composer window</summary>
public class OpenPlaylistComposerMessage
{
    /// <summary>Start from the songs queued in the current playlist instead of an empty composition.</summary>
    public bool WithCurrentQueue { get; init; }
}

/// <summary>Request to show the Properties dialog of a media file</summary>
public record ShowMediaPropertiesMessage(KaraokePlayer.Models.MediaFile File);

/// <summary>Request to open the Settings window</summary>
public class OpenSettingsMessage
{
}
