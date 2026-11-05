using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using LibVLCSharp.Shared;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for controlling media playback with LibVLC
/// </summary>
public interface IMediaPlayerController
{
    /// <summary>
    /// Gets the current playback state
    /// </summary>
    PlaybackState State { get; }

    /// <summary>
    /// Gets the current playback time in seconds
    /// </summary>
    double CurrentTime { get; }

    /// <summary>
    /// Gets the total duration of the current media in seconds
    /// </summary>
    double Duration { get; }

    /// <summary>
    /// Gets the current volume level (0.0 to 1.0)
    /// </summary>
    float Volume { get; }

    /// <summary>
    /// Gets whether subtitles are currently enabled
    /// </summary>
    bool SubtitlesEnabled { get; }

    /// <summary>
    /// Gets the currently playing media file
    /// </summary>
    MediaFile? CurrentMedia { get; }

    /// <summary>
    /// Plays the specified media file
    /// </summary>
    /// <param name="mediaFile">The media file to play</param>
    Task PlayAsync(MediaFile mediaFile);

    /// <summary>
    /// Pauses the current playback
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes playback if paused
    /// </summary>
    void Resume();

    /// <summary>
    /// Stops the current playback
    /// </summary>
    void Stop();

    /// <summary>
    /// Seeks to a specific time in the current media
    /// </summary>
    /// <param name="timeInSeconds">The time to seek to in seconds</param>
    void Seek(double timeInSeconds);

    /// <summary>
    /// Sets the volume level
    /// </summary>
    /// <param name="level">Volume level from 0.0 (mute) to 1.0 (max)</param>
    void SetVolume(float level);

    /// <summary>
    /// Sets the audio output device
    /// </summary>
    /// <param name="deviceId">The device ID to use for audio output</param>
    void SetAudioDevice(string deviceId);

    /// <summary>
    /// Gets the list of available audio output devices
    /// </summary>
    List<AudioDevice> GetAudioDevices();

    /// <summary>Plays a short test tone on an audio device (empty id = system default).</summary>
    void PlayTestTone(string deviceId);

    /// <summary>
    /// Toggles subtitle display on or off
    /// </summary>
    /// <param name="enabled">True to enable subtitles, false to disable</param>
    void ToggleSubtitles(bool enabled);

    /// <summary>
    /// Preloads the next media file for crossfade transitions
    /// </summary>
    /// <param name="mediaFile">The media file to preload</param>
    Task PreloadNextAsync(MediaFile mediaFile);

    /// <summary>
    /// Enables or disables crossfade transitions with configurable duration
    /// </summary>
    /// <param name="enabled">True to enable crossfade, false to disable</param>
    /// <param name="durationSeconds">Crossfade duration in seconds (1-20)</param>
    void EnableCrossfade(bool enabled, int durationSeconds);

    /// <summary>
    /// Volume normalization ("audio boost"): evens out quiet and loud songs.
    /// Applies from the next song that starts.
    /// </summary>
    bool AudioNormalizationEnabled { get; set; }

    /// <summary>
    /// Visualization drawn by LibVLC in the video surface while an audio file plays
    /// ("bars", "waveform", "circular", "particles"; "none" = no visualization).
    /// Applies from the next song that starts.
    /// </summary>
    string AudioVisualization { get; set; }

    /// <summary>
    /// Gets whether crossfade is currently enabled
    /// </summary>
    bool CrossfadeEnabled { get; }

    /// <summary>
    /// Gets the current crossfade duration in seconds
    /// </summary>
    int CrossfadeDuration { get; }

    /// <summary>
    /// Gets the active MediaPlayer instance for video rendering
    /// </summary>
    /// <returns>The currently active MediaPlayer</returns>
    LibVLCSharp.Shared.MediaPlayer GetActiveMediaPlayer();

    /// <summary>
    /// Native windows (HWND / X11 window) for the two players' video (one each:
    /// LibVLC cannot share one window between two players). Takes effect for media
    /// started afterwards.
    /// </summary>
    void AttachVideoWindows(IntPtr first, IntPtr second);

    /// <summary>Index (0 or 1) of the player playing the current song: its video window is the one to show.</summary>
    int ActivePlayerIndex { get; }

    /// <summary>
    /// Forgets the preloaded next song (no crossfade until a new one is preloaded)
    /// </summary>
    void ClearPreload();

    /// <summary>
    /// Event raised when playback state changes
    /// </summary>
    event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when playback time updates
    /// </summary>
    event EventHandler<TimeChangedEventArgs>? TimeChanged;

    /// <summary>
    /// Event raised when the current media ends
    /// </summary>
    event EventHandler<MediaEndedEventArgs>? MediaEnded;

    /// <summary>
    /// Event raised when a playback error occurs
    /// </summary>
    event EventHandler<PlaybackErrorEventArgs>? PlaybackError;

    /// <summary>
    /// A crossfade started: <c>To</c> is now the current song (its video is shown,
    /// <c>From</c> keeps fading out in the background). MediaEnded is not raised for <c>From</c>.
    /// </summary>
    event EventHandler<CrossfadeStartedEventArgs>? CrossfadeStarted;
}

public class CrossfadeStartedEventArgs : EventArgs
{
    public MediaFile? From { get; set; }
    public MediaFile To { get; set; } = null!;
}

/// <summary>
/// Playback state enumeration
/// </summary>
public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
    Buffering,
    Error
}

/// <summary>
/// Audio output device information
/// </summary>
public class AudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Event arguments for playback state changes
/// </summary>
public class PlaybackStateChangedEventArgs : EventArgs
{
    public PlaybackState OldState { get; set; }
    public PlaybackState NewState { get; set; }
}

/// <summary>
/// Event arguments for time changes
/// </summary>
public class TimeChangedEventArgs : EventArgs
{
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
}

/// <summary>
/// Event arguments for media ended
/// </summary>
public class MediaEndedEventArgs : EventArgs
{
    public MediaFile? MediaFile { get; set; }
}

/// <summary>
/// Event arguments for playback errors
/// </summary>
public class PlaybackErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; set; } = string.Empty;
    public MediaFile? MediaFile { get; set; }
    public Exception? Exception { get; set; }
}
