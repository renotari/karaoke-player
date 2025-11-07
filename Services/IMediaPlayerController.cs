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

    /// <summary>
    /// Toggles subtitle display on or off
    /// </summary>
    /// <param name="enabled">True to enable subtitles, false to disable</param>
    void ToggleSubtitles(bool enabled);

    /// <summary>
    /// Gets the current audio spectrum data for visualizations
    /// </summary>
    /// <returns>Array of frequency amplitudes</returns>
    float[] GetAudioSpectrum();

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
