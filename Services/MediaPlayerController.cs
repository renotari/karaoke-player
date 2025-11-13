using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using LibVLCSharp.Shared;

namespace KaraokePlayer.Services;

/// <summary>
/// Controls media playback using LibVLC with dual player instances for crossfade
/// </summary>
public class MediaPlayerController : IMediaPlayerController, IDisposable
{
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _currentPlayer;
    private readonly MediaPlayer _preloadedPlayer;
    private readonly ILoggingService? _loggingService;
    private PlaybackState _state;
    private MediaFile? _currentMedia;
    private MediaFile? _preloadedMedia;
    private float _volume = 0.5f;
    private bool _subtitlesEnabled = true;
    private readonly object _stateLock = new();
    private readonly float[] _audioSpectrum = new float[256];
    
    // Crossfade properties
    private bool _crossfadeEnabled = false;
    private int _crossfadeDuration = 5; // Default 5 seconds
    private bool _crossfadeInProgress = false;
    private System.Threading.Timer? _crossfadeTimer;
    private DateTime _crossfadeStartTime;
    private int _originalCurrentVolume;
    private bool _isPreloadedPlayerActive = false;

    // Crossfade failure protection
    private int _skipAttempts = 0;
    private const int MAX_SKIP_ATTEMPTS = 3;

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

    public double CurrentTime => GetActivePlayer().Time / 1000.0;

    public double Duration => GetActivePlayer().Length / 1000.0;

    public float Volume => _volume;

    public bool SubtitlesEnabled => _subtitlesEnabled;

    public MediaFile? CurrentMedia => _currentMedia;

    public bool CrossfadeEnabled => _crossfadeEnabled;

    public int CrossfadeDuration => _crossfadeDuration;

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<TimeChangedEventArgs>? TimeChanged;
    public event EventHandler<MediaEndedEventArgs>? MediaEnded;
    public event EventHandler<PlaybackErrorEventArgs>? PlaybackError;

    public MediaPlayerController(ILoggingService? loggingService = null)
    {
        _loggingService = loggingService;
        
        // Initialize LibVLC
        Core.Initialize();
        
        _libVLC = new LibVLC();
        _currentPlayer = new MediaPlayer(_libVLC);
        _preloadedPlayer = new MediaPlayer(_libVLC);

        // Wire up events for current player
        _currentPlayer.Playing += OnPlaying;
        _currentPlayer.Paused += OnPaused;
        _currentPlayer.Stopped += OnStopped;
        _currentPlayer.EndReached += OnEndReached;
        _currentPlayer.EncounteredError += OnEncounteredError;
        _currentPlayer.TimeChanged += OnTimeChanged;
        _currentPlayer.Buffering += OnBuffering;

        // Set initial volume
        _currentPlayer.Volume = (int)(_volume * 100);
        _preloadedPlayer.Volume = 0; // Preloaded player starts muted
        
        _loggingService?.LogInformation("MediaPlayerController initialized");
    }

    public async Task PlayAsync(MediaFile mediaFile)
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        await Task.Run(() =>
        {
            try
            {
                // Cancel any ongoing crossfade
                if (_crossfadeInProgress)
                {
                    CancelCrossfade();
                }

                var activePlayer = GetActivePlayer();

                // Stop current playback
                if (activePlayer.IsPlaying)
                {
                    activePlayer.Stop();
                }

                // Create media from file path
                var media = new Media(_libVLC, mediaFile.FilePath, FromType.FromPath);
                
                // Set media to active player
                activePlayer.Media = media;

                // Store current media reference
                _currentMedia = mediaFile;

                // Apply subtitle settings
                if (!_subtitlesEnabled && activePlayer.SpuCount > 0)
                {
                    activePlayer.SetSpu(-1); // Disable subtitles
                }

                // Restore volume to active player
                activePlayer.Volume = (int)(_volume * 100);

                // Start playback
                activePlayer.Play();

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
        var activePlayer = GetActivePlayer();
        if (activePlayer.IsPlaying)
        {
            activePlayer.Pause();
        }
    }

    public void Resume()
    {
        var activePlayer = GetActivePlayer();
        if (activePlayer.CanPause && !activePlayer.IsPlaying)
        {
            activePlayer.Play();
        }
    }

    public void Stop()
    {
        // Cancel any ongoing crossfade
        if (_crossfadeInProgress)
        {
            CancelCrossfade();
        }

        var activePlayer = GetActivePlayer();
        activePlayer.Stop();
        _currentMedia = null;
        _preloadedMedia = null;
        State = PlaybackState.Stopped;
    }

    public void Seek(double timeInSeconds)
    {
        var activePlayer = GetActivePlayer();
        if (activePlayer.IsSeekable)
        {
            var timeInMs = (long)(timeInSeconds * 1000);
            activePlayer.Time = timeInMs;
        }
    }

    public void SetVolume(float level)
    {
        if (level < 0.0f || level > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(level), "Volume must be between 0.0 and 1.0");

        _volume = level;
        
        // Only update active player volume if not in crossfade
        if (!_crossfadeInProgress)
        {
            var activePlayer = GetActivePlayer();
            activePlayer.Volume = (int)(level * 100);
        }
    }

    public void SetAudioDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));

        try
        {
            _currentPlayer.SetAudioOutput(deviceId);
            _preloadedPlayer.SetAudioOutput(deviceId);
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
            {
                ErrorMessage = $"Failed to set audio device: {ex.Message}",
                Exception = ex
            });
        }
    }

    public List<AudioDevice> GetAudioDevices()
    {
        var devices = new List<AudioDevice>();

        try
        {
            var audioOutputs = _libVLC.AudioOutputs;
            
            foreach (var output in audioOutputs)
            {
                devices.Add(new AudioDevice
                {
                    Id = output.Name,
                    Name = output.Description
                });
            }
        }
        catch (Exception)
        {
            // If we can't get devices, return empty list
        }

        return devices;
    }

    public void ToggleSubtitles(bool enabled)
    {
        _subtitlesEnabled = enabled;

        var activePlayer = GetActivePlayer();
        if (activePlayer.Media != null)
        {
            if (enabled && activePlayer.SpuCount > 0)
            {
                // Enable first subtitle track
                activePlayer.SetSpu(0);
            }
            else
            {
                // Disable subtitles
                activePlayer.SetSpu(-1);
            }
        }
    }

    public float[] GetAudioSpectrum()
    {
        // Note: LibVLC doesn't provide direct audio spectrum access in the same way as Web Audio API
        // This is a placeholder that returns the spectrum array
        // In a full implementation, you would need to use LibVLC audio callbacks
        // or implement a custom audio filter to capture spectrum data
        
        // For now, return zeros (visualization will need to be implemented separately)
        return _audioSpectrum;
    }

    public async Task PreloadNextAsync(MediaFile mediaFile)
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        await Task.Run(() =>
        {
            try
            {
                var inactivePlayer = GetInactivePlayer();

                // Stop any existing preloaded media
                if (inactivePlayer.IsPlaying)
                {
                    inactivePlayer.Stop();
                }

                // Create media from file path
                var media = new Media(_libVLC, mediaFile.FilePath, FromType.FromPath);
                
                // Set media to inactive player (for preloading)
                inactivePlayer.Media = media;

                // Store preloaded media reference
                _preloadedMedia = mediaFile;

                // Parse the media to get metadata without playing
                media.Parse(MediaParseOptions.ParseNetwork);
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

    #region LibVLC Event Handlers

    private void OnPlaying(object? sender, EventArgs e)
    {
        State = PlaybackState.Playing;
        _skipAttempts = 0; // Reset skip counter on successful playback
        _loggingService?.LogPlaybackEvent("Playing", $"File: {_currentMedia?.Filename ?? "Unknown"}");
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        State = PlaybackState.Paused;
        _loggingService?.LogPlaybackEvent("Paused", $"File: {_currentMedia?.Filename ?? "Unknown"}");
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        State = PlaybackState.Stopped;
        _loggingService?.LogPlaybackEvent("Stopped", $"File: {_currentMedia?.Filename ?? "Unknown"}");
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
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
        var currentTime = e.Time / 1000.0;
        var duration = Duration;
        
        TimeChanged?.Invoke(this, new TimeChangedEventArgs
        {
            CurrentTime = currentTime,
            Duration = duration
        });

        // Check if we should trigger crossfade
        if (_crossfadeEnabled && !_crossfadeInProgress && _preloadedMedia != null && duration > 0)
        {
            var timeRemaining = duration - currentTime;
            
            // Trigger crossfade at the configured time before song end
            if (timeRemaining <= _crossfadeDuration && timeRemaining > 0)
            {
                StartCrossfade();
            }
        }
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (e.Cache < 100)
        {
            State = PlaybackState.Buffering;
        }
        else if (_currentPlayer.IsPlaying)
        {
            State = PlaybackState.Playing;
        }
    }

    #endregion

    #region Helper Methods

    private MediaPlayer GetActivePlayer()
    {
        return _isPreloadedPlayerActive ? _preloadedPlayer : _currentPlayer;
    }

    private MediaPlayer GetInactivePlayer()
    {
        return _isPreloadedPlayerActive ? _currentPlayer : _preloadedPlayer;
    }

    /// <summary>
    /// Gets the active MediaPlayer instance for video rendering
    /// </summary>
    /// <returns>The currently active MediaPlayer</returns>
    public MediaPlayer GetActiveMediaPlayer()
    {
        return GetActivePlayer();
    }

    #endregion

    #region Crossfade Implementation

    public void EnableCrossfade(bool enabled, int durationSeconds)
    {
        if (durationSeconds < 1 || durationSeconds > 20)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Crossfade duration must be between 1 and 20 seconds");

        _crossfadeEnabled = enabled;
        _crossfadeDuration = durationSeconds;
    }

    private void StartCrossfade()
    {
        if (_crossfadeInProgress || _preloadedMedia == null)
            return;

        try
        {
            _crossfadeInProgress = true;
            _crossfadeStartTime = DateTime.Now;
            
            var activePlayer = GetActivePlayer();
            var inactivePlayer = GetInactivePlayer();
            
            _originalCurrentVolume = activePlayer.Volume;

            // Start playing the preloaded media at volume 0
            inactivePlayer.Volume = 0;
            inactivePlayer.Play();

            _loggingService?.LogPlaybackEvent("CrossfadeStarted", 
                $"From: {_currentMedia?.Filename ?? "Unknown"} To: {_preloadedMedia?.Filename ?? "Unknown"} Duration: {_crossfadeDuration}s");

            // Create a timer to update volumes during crossfade
            _crossfadeTimer = new System.Threading.Timer(UpdateCrossfadeVolumes, null, 0, 50); // Update every 50ms
        }
        catch (Exception ex)
        {
            // Protect against infinite skip loop with unplayable files
            _skipAttempts++;

            if (_skipAttempts >= MAX_SKIP_ATTEMPTS)
            {
                _loggingService?.LogError($"Too many consecutive crossfade failures ({_skipAttempts}). Stopping playback.", ex);

                CancelCrossfade();
                Stop();
                _skipAttempts = 0;

                PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
                {
                    ErrorMessage = $"Too many consecutive playback failures. Please check your media files.",
                    MediaFile = _preloadedMedia,
                    Exception = ex
                });
                return;
            }

            // If crossfade fails, cancel it and skip to next song
            _loggingService?.LogCrossfadeTransition(
                _currentMedia?.Filename ?? "Unknown",
                _preloadedMedia?.Filename ?? "Unknown",
                false,
                ex.Message
            );

            CancelCrossfade();

            PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
            {
                ErrorMessage = $"Crossfade failed: {ex.Message}",
                MediaFile = _preloadedMedia,
                Exception = ex
            });

            // Skip to the next valid song by ending current playback
            var activePlayer = GetActivePlayer();
            activePlayer.Stop();
        }
    }

    private void UpdateCrossfadeVolumes(object? state)
    {
        if (!_crossfadeInProgress)
            return;

        try
        {
            var elapsed = (DateTime.Now - _crossfadeStartTime).TotalSeconds;
            var progress = Math.Min(elapsed / _crossfadeDuration, 1.0);

            if (progress >= 1.0)
            {
                // Crossfade complete
                CompleteCrossfade();
                return;
            }

            var activePlayer = GetActivePlayer();
            var inactivePlayer = GetInactivePlayer();

            // Calculate fade curves (linear for simplicity, could use ease-in/out)
            var fadeOutVolume = (int)(_originalCurrentVolume * (1.0 - progress));
            var fadeInVolume = (int)(_originalCurrentVolume * progress);

            // Apply volumes - active player fades out, inactive player fades in
            activePlayer.Volume = Math.Max(0, fadeOutVolume);
            inactivePlayer.Volume = Math.Min(100, fadeInVolume);
        }
        catch (Exception ex)
        {
            // If volume update fails, cancel crossfade
            CancelCrossfade();
            
            PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
            {
                ErrorMessage = $"Crossfade volume update failed: {ex.Message}",
                Exception = ex
            });
        }
    }

    private void CompleteCrossfade()
    {
        try
        {
            // Stop the timer
            _crossfadeTimer?.Dispose();
            _crossfadeTimer = null;

            var oldMedia = _currentMedia;
            var activePlayer = GetActivePlayer();

            // Stop the old player
            activePlayer.Stop();

            // Swap the players - preloaded becomes current
            SwapPlayers();

            // Reset crossfade state
            _crossfadeInProgress = false;
            _preloadedMedia = null;

            _loggingService?.LogCrossfadeTransition(
                oldMedia?.Filename ?? "Unknown",
                _currentMedia?.Filename ?? "Unknown",
                true
            );

            // Notify that the previous media has ended
            MediaEnded?.Invoke(this, new MediaEndedEventArgs
            {
                MediaFile = oldMedia
            });
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to complete crossfade: {ex.Message}", ex);
            
            PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
            {
                ErrorMessage = $"Failed to complete crossfade: {ex.Message}",
                Exception = ex
            });
        }
    }

    private void CancelCrossfade()
    {
        _crossfadeTimer?.Dispose();
        _crossfadeTimer = null;
        _crossfadeInProgress = false;

        var activePlayer = GetActivePlayer();
        var inactivePlayer = GetInactivePlayer();

        // Stop inactive player if it's playing
        if (inactivePlayer.IsPlaying)
        {
            inactivePlayer.Stop();
        }

        // Restore active player volume
        activePlayer.Volume = _originalCurrentVolume;
        
        _preloadedMedia = null;
    }

    private void SwapPlayers()
    {
        // The preloaded player is now the active player
        // We need to swap event handlers and references
        
        // Unsubscribe from current player events
        _currentPlayer.Playing -= OnPlaying;
        _currentPlayer.Paused -= OnPaused;
        _currentPlayer.Stopped -= OnStopped;
        _currentPlayer.EndReached -= OnEndReached;
        _currentPlayer.EncounteredError -= OnEncounteredError;
        _currentPlayer.TimeChanged -= OnTimeChanged;
        _currentPlayer.Buffering -= OnBuffering;

        // Subscribe to preloaded player events
        _preloadedPlayer.Playing -= OnPlaying;
        _preloadedPlayer.Paused -= OnPaused;
        _preloadedPlayer.Stopped -= OnStopped;
        _preloadedPlayer.EndReached -= OnEndReached;
        _preloadedPlayer.EncounteredError -= OnEncounteredError;
        _preloadedPlayer.TimeChanged -= OnTimeChanged;
        _preloadedPlayer.Buffering -= OnBuffering;

        _preloadedPlayer.Playing += OnPlaying;
        _preloadedPlayer.Paused += OnPaused;
        _preloadedPlayer.Stopped += OnStopped;
        _preloadedPlayer.EndReached += OnEndReached;
        _preloadedPlayer.EncounteredError += OnEncounteredError;
        _preloadedPlayer.TimeChanged += OnTimeChanged;
        _preloadedPlayer.Buffering += OnBuffering;

        // Re-subscribe to current player events for next use
        _currentPlayer.Playing += OnPlaying;
        _currentPlayer.Paused += OnPaused;
        _currentPlayer.Stopped += OnStopped;
        _currentPlayer.EndReached += OnEndReached;
        _currentPlayer.EncounteredError += OnEncounteredError;
        _currentPlayer.TimeChanged += OnTimeChanged;
        _currentPlayer.Buffering += OnBuffering;

        // Update current media reference
        _currentMedia = _preloadedMedia;

        // Toggle which player is active
        _isPreloadedPlayerActive = !_isPreloadedPlayerActive;
    }

    #endregion

    public void Dispose()
    {
        // Cancel any ongoing crossfade
        if (_crossfadeInProgress)
        {
            CancelCrossfade();
        }

        // Dispose crossfade timer
        _crossfadeTimer?.Dispose();

        // Unsubscribe from events
        _currentPlayer.Playing -= OnPlaying;
        _currentPlayer.Paused -= OnPaused;
        _currentPlayer.Stopped -= OnStopped;
        _currentPlayer.EndReached -= OnEndReached;
        _currentPlayer.EncounteredError -= OnEncounteredError;
        _currentPlayer.TimeChanged -= OnTimeChanged;
        _currentPlayer.Buffering -= OnBuffering;

        _preloadedPlayer.Playing -= OnPlaying;
        _preloadedPlayer.Paused -= OnPaused;
        _preloadedPlayer.Stopped -= OnStopped;
        _preloadedPlayer.EndReached -= OnEndReached;
        _preloadedPlayer.EncounteredError -= OnEncounteredError;
        _preloadedPlayer.TimeChanged -= OnTimeChanged;
        _preloadedPlayer.Buffering -= OnBuffering;

        // Stop playback
        _currentPlayer.Stop();
        _preloadedPlayer.Stop();

        // Dispose players
        _currentPlayer.Dispose();
        _preloadedPlayer.Dispose();

        // Dispose LibVLC
        _libVLC.Dispose();
    }
}
