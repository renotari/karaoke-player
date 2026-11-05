namespace KaraokePlayer.Services;

// Messages sent through ReactiveUI's MessageBus from the ViewModel to the main window
// (which opens the corresponding windows).

/// <summary>Request to open the Playlist Composer window</summary>
public class OpenPlaylistComposerMessage
{
}

/// <summary>Request to open the Settings window</summary>
public class OpenSettingsMessage
{
}
