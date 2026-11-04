using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using LibVLCSharp.Shared;

namespace KaraokePlayer.Services;

/// <summary>
/// Controls media playback using LibVLC.
///
/// Two MediaPlayer instances are used for crossfade: the "active" one plays the
/// current song, the other one holds the preloaded next song. Each renders into its
/// own native video window (<see cref="AttachVideoWindows"/>); during a crossfade the
/// incoming song becomes active immediately (the UI switches to its window: a real
/// video fade is not possible on a native surface) while the two audio tracks
/// overlap and fade. Events coming from the non-active player are ignored.
///
/// LibVLC callbacks must not call back into LibVLC on the same thread, so work
/// triggered from its events is moved to the thread pool.
/// </summary>
public class MediaPlayerController : IMediaPlayerController, IDisposable
{
    public const string DefaultAudioDeviceName = "Default Audio Device";

    private readonly LibVLC _libVLC;
    private readonly MediaPlayer[] _players;
    private int _active; // index of the active player in _players
    private readonly ILoggingService? _loggingService;
    // _sync guards player/crossfade state. LibVLC event handlers must never take it:
    // Stop() blocks until LibVLC has delivered its events, so a handler waiting on
    // _sync would deadlock. The playback state uses its own lock.
    private readonly object _sync = new();
    private readonly object _stateLock = new();

    private PlaybackState _state;
    private MediaFile? _currentMedia;
    private MediaFile? _preloadedMedia;
    private MediaFile? _pendingPreload; // requested while a crossfade was using the other player
    private float _volume = 0.5f;
    private bool _subtitlesEnabled = true;
    private readonly float[] _audioSpectrum = new float[256];
    private string? _audioDeviceId; // null = system default

    // Crossfade
    private bool _crossfadeEnabled;
    private int _crossfadeDuration = 5;
    private bool _crossfadeInProgress;
    private Timer? _crossfadeTimer;
    private DateTime _crossfadeStartTime;
    private bool _disposed;

    public MediaPlayerController(ILoggingService? loggingService = null)
    {
        _loggingService = loggingService;

        Core.Initialize();
        _libVLC = new LibVLC("--no-video-title-show");

        _players = new[] { new MediaPlayer(_libVLC), new MediaPlayer(_libVLC) };
        foreach (var player in _players)
        {
            Subscribe(player);
        }

        Active.Volume = (int)(_volume * 100);
        Other.Volume = 0;

        _loggingService?.LogInformation("MediaPlayerController initialized");
    }

    // LibVLCSharp raises MediaPlayer events with its internal event manager as the
    // sender, not the MediaPlayer: subscribe through closures that pass the player.
    private readonly List<Action> _unsubscribe = new();

    private void Subscribe(MediaPlayer player)
    {
        EventHandler<EventArgs> playing = (_, e) => OnPlaying(player, e);
        EventHandler<EventArgs> paused = (_, e) => OnPaused(player, e);
        EventHandler<EventArgs> stopped = (_, e) => OnStopped(player, e);
        EventHandler<EventArgs> endReached = (_, e) => OnEndReached(player, e);
        EventHandler<EventArgs> error = (_, e) => OnEncounteredError(player, e);
        EventHandler<MediaPlayerTimeChangedEventArgs> timeChanged = (_, e) => OnTimeChanged(player, e);
        EventHandler<MediaPlayerBufferingEventArgs> buffering = (_, e) => OnBuffering(player, e);

        player.Playing += playing;
        player.Paused += paused;
        player.Stopped += stopped;
        player.EndReached += endReached;
        player.EncounteredError += error;
        player.TimeChanged += timeChanged;
        player.Buffering += buffering;

        _unsubscribe.Add(() =>
        {
            player.Playing -= playing;
            player.Paused -= paused;
            player.Stopped -= stopped;
            player.EndReached -= endReached;
            player.EncounteredError -= error;
            player.TimeChanged -= timeChanged;
            player.Buffering -= buffering;
        });
    }

    private MediaPlayer Active => _players[_active];
    private MediaPlayer Other => _players[1 - _active];
    private bool IsActive(object? sender) => ReferenceEquals(sender, Active);

    #region Properties and events

    public PlaybackState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            PlaybackState oldState;
            lock (_stateLock)
            {
                oldState = _state;
                _state = value;
            }

            if (oldState != value)
            {
                StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
                {
                    OldState = oldState,
                    NewState = value
                });
            }
        }
    }

    // LibVLC returns -1 when no media is loaded: clamp to 0
    public double CurrentTime => Math.Max(0, Active.Time) / 1000.0;

    public double Duration => Math.Max(0, Active.Length) / 1000.0;

    public float Volume => _volume;

    public bool SubtitlesEnabled => _subtitlesEnabled;

    public MediaFile? CurrentMedia => _currentMedia;

    public bool CrossfadeEnabled => _crossfadeEnabled;

    public int CrossfadeDuration => _crossfadeDuration;

    public bool AudioNormalizationEnabled { get; set; }

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<TimeChangedEventArgs>? TimeChanged;
    public event EventHandler<MediaEndedEventArgs>? MediaEnded;
    public event EventHandler<PlaybackErrorEventArgs>? PlaybackError;
    public event EventHandler<CrossfadeStartedEventArgs>? CrossfadeStarted;

    #endregion

    #region Playback

    public async Task PlayAsync(MediaFile mediaFile)
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        await Task.Run(() =>
        {
            try
            {
                lock (_sync)
                {
                    CancelCrossfade();

                    var player = Active;
                    player.Stop();

                    var previousMedia = player.Media;
                    player.Media = CreateMedia(mediaFile);
                    previousMedia?.Dispose();

                    _currentMedia = mediaFile;
                    if (_preloadedMedia?.Id == mediaFile.Id)
                    {
                        // The preloaded song is being played directly: the queue will preload a new one
                        _preloadedMedia = null;
                    }

                    player.Volume = (int)(_volume * 100);
                    player.Play();
                    ApplyAudioDevice(player);
                }

                State = PlaybackState.Buffering;
            }
            catch (Exception ex)
            {
                State = PlaybackState.Error;
                PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
                {
                    ErrorMessage = $"Failed to play media: {ex.Message}",
                    MediaFile = mediaFile,
                    Exception = ex
                });
            }
        });
    }

    public void Pause()
    {
        lock (_sync)
        {
            // Pausing in the middle of a crossfade: finish it (drop the outgoing song)
            CancelCrossfade();

            if (Active.IsPlaying)
            {
                Active.Pause();
            }
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (Active.Media != null && !Active.IsPlaying)
            {
                Active.SetPause(false);
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            CancelCrossfade();
            Active.Stop();
            _currentMedia = null;
        }

        State = PlaybackState.Stopped;
    }

    public void Seek(double timeInSeconds)
    {
        var player = Active;
        if (player.IsSeekable)
        {
            player.Time = (long)(Math.Max(0, timeInSeconds) * 1000);
        }
    }

    public void SetVolume(float level)
    {
        if (level < 0.0f || level > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(level), "Volume must be between 0.0 and 1.0");

        _volume = level;

        // During a crossfade the fade timer drives the volumes (it reads _volume)
        if (!_crossfadeInProgress)
        {
            Active.Volume = (int)(level * 100);
        }
    }

    public void ToggleSubtitles(bool enabled)
    {
        _subtitlesEnabled = enabled;

        var player = Active;
        if (player.Media != null)
        {
            if (enabled && player.SpuCount > 0)
            {
                // Enable first subtitle track
                player.SetSpu(0);
            }
            else
            {
                player.SetSpu(-1);
            }
        }
    }

    public float[] GetAudioSpectrum()
    {
        // LibVLC does not expose the audio spectrum without replacing the audio output;
        // the visualization engine animates without real spectrum data.
        return _audioSpectrum;
    }

    private Media CreateMedia(MediaFile mediaFile)
    {
        var media = new Media(_libVLC, mediaFile.FilePath, FromType.FromPath);

        if (AudioNormalizationEnabled)
        {
            media.AddOption(":audio-filter=normvol");
        }

        if (mediaFile.Type == Models.MediaType.Audio)
        {
            // Do not decode embedded cover art as a video track
            media.AddOption(":no-video");
        }

        return media;
    }

    #endregion

    #region Video window and audio device

    /// <summary>
    /// Native windows (HWND on Windows, X11 window on Linux) the two players render
    /// into: player 0 into <paramref name="first"/>, player 1 into <paramref name="second"/>.
    /// LibVLC cannot reuse a window another player has just used, so each player needs
    /// its own; the UI shows the one of <see cref="ActivePlayerIndex"/>.
    /// Takes effect for media started afterwards.
    /// </summary>
    public void AttachVideoWindows(IntPtr first, IntPtr second)
    {
        lock (_sync)
        {
            SetVideoWindow(_players[0], first);
            SetVideoWindow(_players[1], second);
        }
    }

    /// <summary>Index (0 or 1) of the player playing the current song.</summary>
    public int ActivePlayerIndex => _active;

    private static void SetVideoWindow(MediaPlayer player, IntPtr handle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            player.Hwnd = handle;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            player.XWindow = (uint)handle.ToInt64();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            player.NsObject = handle;
        }
    }

    /// <summary>
    /// Selects the audio output device (speakers, HDMI, USB...). An empty id means
    /// the system default. The choice is re-applied every time a song starts,
    /// because LibVLC can reset it when the audio output is recreated.
    /// </summary>
    public void SetAudioDevice(string deviceId)
    {
        _audioDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
        foreach (var player in _players)
        {
            ApplyAudioDevice(player);
        }
    }

    private void ApplyAudioDevice(MediaPlayer player)
    {
        if (_audioDeviceId == null)
            return;

        try
        {
            player.SetOutputDevice(_audioDeviceId);
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning($"Failed to set audio device '{_audioDeviceId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Audio output devices reported by LibVLC. The first entry (empty id) is the system default.
    /// </summary>
    public List<AudioDevice> GetAudioDevices()
    {
        var devices = new List<AudioDevice>
        {
            new AudioDevice { Id = string.Empty, Name = DefaultAudioDeviceName }
        };

        try
        {
            foreach (var device in Active.AudioOutputDeviceEnum)
            {
                if (string.IsNullOrEmpty(device.DeviceIdentifier))
                    continue;

                devices.Add(new AudioDevice
                {
                    Id = device.DeviceIdentifier,
                    Name = string.IsNullOrWhiteSpace(device.Description) ? device.DeviceIdentifier : device.Description
                });
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning($"Could not list audio devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>The player currently rendering video.</summary>
    public MediaPlayer GetActiveMediaPlayer() => Active;

    #endregion

    #region Preload and crossfade

    public void EnableCrossfade(bool enabled, int durationSeconds)
    {
        if (durationSeconds < 1 || durationSeconds > 20)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Crossfade duration must be between 1 and 20 seconds");

        _crossfadeEnabled = enabled;
        _crossfadeDuration = durationSeconds;

        if (!enabled)
        {
            lock (_sync)
            {
                CancelCrossfade();
            }
        }
    }

    /// <summary>Loads the next song in the second player so a crossfade can start instantly.</summary>
    public async Task PreloadNextAsync(MediaFile mediaFile)
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        await Task.Run(() =>
        {
            try
            {
                lock (_sync)
                {
                    if (_crossfadeInProgress)
                    {
                        // The other player is still fading out the previous song
                        _pendingPreload = mediaFile;
                        return;
                    }

                    if (_preloadedMedia?.Id == mediaFile.Id && Other.Media != null)
                        return;

                    LoadInto(Other, mediaFile);
                }
            }
            catch (Exception ex)
            {
                _preloadedMedia = null;
                PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
                {
                    ErrorMessage = $"Failed to preload media: {ex.Message}",
                    MediaFile = mediaFile,
                    Exception = ex
                });
            }
        });
    }

    /// <summary>Forgets the preloaded song (the queue has no next song any more).</summary>
    public void ClearPreload()
    {
        lock (_sync)
        {
            _pendingPreload = null;
            if (!_crossfadeInProgress)
            {
                _preloadedMedia = null;
            }
        }
    }

    private void LoadInto(MediaPlayer player, MediaFile mediaFile)
    {
        player.Stop();
        var previousMedia = player.Media;
        player.Media = CreateMedia(mediaFile);
        previousMedia?.Dispose();
        player.Volume = 0;
        _preloadedMedia = mediaFile;
    }

    private void StartCrossfade()
    {
        MediaFile? from, to;

        lock (_sync)
        {
            if (_crossfadeInProgress || _preloadedMedia == null || !_crossfadeEnabled || _disposed)
                return;

            var outgoing = Active;
            var incoming = Other;

            try
            {
                incoming.Volume = 0;
                incoming.Play();
                ApplyAudioDevice(incoming);
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Crossfade could not start", ex);
                _preloadedMedia = null;
                return; // the current song simply plays to its end
            }

            from = _currentMedia;
            to = _preloadedMedia;

            _active = 1 - _active;
            _currentMedia = to;
            _preloadedMedia = null;
            _crossfadeInProgress = true;
            _crossfadeStartTime = DateTime.UtcNow;
            _crossfadeTimer = new Timer(_ => UpdateCrossfade(), null, 0, 50);
        }

        _loggingService?.LogPlaybackEvent("CrossfadeStarted",
            $"From: {from?.Filename ?? "Unknown"} To: {to?.Filename ?? "Unknown"} Duration: {_crossfadeDuration}s");

        CrossfadeStarted?.Invoke(this, new CrossfadeStartedEventArgs { From = from, To = to! });
    }

    private void UpdateCrossfade()
    {
        lock (_sync)
        {
            if (!_crossfadeInProgress)
                return;

            var progress = Math.Min((DateTime.UtcNow - _crossfadeStartTime).TotalSeconds / _crossfadeDuration, 1.0);
            var target = _volume * 100;

            try
            {
                Active.Volume = (int)(target * progress);       // incoming
                Other.Volume = (int)(target * (1.0 - progress)); // outgoing
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Crossfade volume update failed", ex);
                progress = 1.0;
            }

            if (progress >= 1.0)
            {
                CancelCrossfade();
            }
        }
    }

    /// <summary>
    /// Ends a crossfade (if any): stops the outgoing song and restores the volume.
    /// Must be called with _sync held.
    /// </summary>
    private void CancelCrossfade()
    {
        if (!_crossfadeInProgress)
            return;

        _crossfadeTimer?.Dispose();
        _crossfadeTimer = null;
        _crossfadeInProgress = false;

        var outgoing = Other;
        // Stop on the thread pool: this may run on the timer thread, never on a LibVLC thread
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { outgoing.Stop(); } catch { /* already stopped */ }
        });
        Active.Volume = (int)(_volume * 100);

        if (_pendingPreload != null)
        {
            var next = _pendingPreload;
            _pendingPreload = null;
            _ = PreloadNextAsync(next);
        }
    }

    #endregion

    #region LibVLC events

    private void OnPlaying(object? sender, EventArgs e)
    {
        if (!IsActive(sender)) return;
        State = PlaybackState.Playing;
        _loggingService?.LogPlaybackEvent("Playing", $"File: {_currentMedia?.Filename ?? "Unknown"}");
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        if (!IsActive(sender)) return;
        State = PlaybackState.Paused;
        _loggingService?.LogPlaybackEvent("Paused", $"File: {_currentMedia?.Filename ?? "Unknown"}");
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        if (!IsActive(sender)) return;
        State = PlaybackState.Stopped;
        _loggingService?.LogPlaybackEvent("Stopped", $"File: {_currentMedia?.Filename ?? "Unknown"}");
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        // The outgoing song of a crossfade ending is expected: ignore it
        if (!IsActive(sender)) return;

        var endedMedia = _currentMedia;
        State = PlaybackState.Stopped;
        _loggingService?.LogPlaybackEvent("EndReached", $"File: {endedMedia?.Filename ?? "Unknown"}");

        MediaEnded?.Invoke(this, new MediaEndedEventArgs
        {
            MediaFile = endedMedia
        });
    }

    private void OnEncounteredError(object? sender, EventArgs e)
    {
        if (!IsActive(sender)) return;

        State = PlaybackState.Error;
        _loggingService?.LogError($"Playback error for file: {_currentMedia?.Filename ?? "Unknown"}");

        PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
        {
            ErrorMessage = "LibVLC encountered a playback error",
            MediaFile = _currentMedia
        });
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        if (!IsActive(sender)) return;

        var currentTime = e.Time / 1000.0;
        var duration = Duration;

        TimeChanged?.Invoke(this, new TimeChangedEventArgs
        {
            CurrentTime = currentTime,
            Duration = duration
        });

        if (_crossfadeEnabled && !_crossfadeInProgress && _preloadedMedia != null && duration > 0)
        {
            var timeRemaining = duration - currentTime;
            if (timeRemaining <= _crossfadeDuration && timeRemaining > 0)
            {
                // Not on the LibVLC event thread
                ThreadPool.QueueUserWorkItem(_ => StartCrossfade());
            }
        }
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (!IsActive(sender)) return;

        if (e.Cache < 100)
        {
            State = PlaybackState.Buffering;
        }
        else if (Active.IsPlaying)
        {
            State = PlaybackState.Playing;
        }
    }

    #endregion

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;

            _crossfadeTimer?.Dispose();
            _crossfadeTimer = null;
            _crossfadeInProgress = false;
        }

        foreach (var unsubscribe in _unsubscribe)
        {
            unsubscribe();
        }

        foreach (var player in _players)
        {
            player.Stop();
            player.Media?.Dispose();
            player.Dispose();
        }

        _libVLC.Dispose();
    }
}
