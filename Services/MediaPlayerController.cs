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
    private PlaybackState _state;
    private MediaFile? _currentMedia;
    private MediaFile? _preloadedMedia;
    private float _volume = 0.5f;
    private bool _subtitlesEnabled = true;
    private readonly object _stateLock = new();
    private readonly float[] _audioSpectrum = new float[256];

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

    public double CurrentTime => _currentPlayer.Time / 1000.0;

    public double Duration => _currentPlayer.Length / 1000.0;

    public float Volume => _volume;

    public bool SubtitlesEnabled => _subtitlesEnabled;

    public MediaFile? CurrentMedia => _currentMedia;

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<TimeChangedEventArgs>? TimeChanged;
    public event EventHandler<MediaEndedEventArgs>? MediaEnded;
    public event EventHandler<PlaybackErrorEventArgs>? PlaybackError;

    public MediaPlayerController()
    {
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
    }

    public async Task PlayAsync(MediaFile mediaFile)
    {
        if (mediaFile == null)
            throw new ArgumentNullException(nameof(mediaFile));

        await Task.Run(() =>
        {
            try
            {
                // Stop current playback
                if (_currentPlayer.IsPlaying)
                {
                    _currentPlayer.Stop();
                }

                // Create media from file path
                var media = new Media(_libVLC, mediaFile.FilePath, FromType.FromPath);
                
                // Set media to player
                _currentPlayer.Media = media;

                // Store current media reference
                _currentMedia = mediaFile;

                // Apply subtitle settings
                if (!_subtitlesEnabled && _currentPlayer.SpuCount > 0)
                {
                    _currentPlayer.SetSpu(-1); // Disable subtitles
                }

                // Start playback
                _currentPlayer.Play();

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
        if (_currentPlayer.IsPlaying)
        {
            _currentPlayer.Pause();
        }
    }

    public void Resume()
    {
        if (_currentPlayer.CanPause && !_currentPlayer.IsPlaying)
        {
            _currentPlayer.Play();
        }
    }

    public void Stop()
    {
        _currentPlayer.Stop();
        _currentMedia = null;
        State = PlaybackState.Stopped;
    }

    public void Seek(double timeInSeconds)
    {
        if (_currentPlayer.IsSeekable)
        {
            var timeInMs = (long)(timeInSeconds * 1000);
            _currentPlayer.Time = timeInMs;
        }
    }

    public void SetVolume(float level)
    {
        if (level < 0.0f || level > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(level), "Volume must be between 0.0 and 1.0");

        _volume = level;
        _currentPlayer.Volume = (int)(level * 100);
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

        if (_currentPlayer.Media != null)
        {
            if (enabled && _currentPlayer.SpuCount > 0)
            {
                // Enable first subtitle track
                _currentPlayer.SetSpu(0);
            }
            else
            {
                // Disable subtitles
                _currentPlayer.SetSpu(-1);
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
                // Stop any existing preloaded media
                if (_preloadedPlayer.IsPlaying)
                {
                    _preloadedPlayer.Stop();
                }

                // Create media from file path
                var media = new Media(_libVLC, mediaFile.FilePath, FromType.FromPath);
                
                // Set media to preloaded player
                _preloadedPlayer.Media = media;

                // Store preloaded media reference
                _preloadedMedia = mediaFile;

                // Parse the media to get metadata without playing
                media.Parse(MediaParseOptions.ParseNetwork);
            }
            catch (Exception ex)
            {
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
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        State = PlaybackState.Paused;
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        State = PlaybackState.Stopped;
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        var endedMedia = _currentMedia;
        State = PlaybackState.Stopped;
        
        MediaEnded?.Invoke(this, new MediaEndedEventArgs
        {
            MediaFile = endedMedia
        });
    }

    private void OnEncounteredError(object? sender, EventArgs e)
    {
        State = PlaybackState.Error;
        
        PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
        {
            ErrorMessage = "LibVLC encountered a playback error",
            MediaFile = _currentMedia
        });
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        TimeChanged?.Invoke(this, new TimeChangedEventArgs
        {
            CurrentTime = e.Time / 1000.0,
            Duration = Duration
        });
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

    public void Dispose()
    {
        // Unsubscribe from events
        _currentPlayer.Playing -= OnPlaying;
        _currentPlayer.Paused -= OnPaused;
        _currentPlayer.Stopped -= OnStopped;
        _currentPlayer.EndReached -= OnEndReached;
        _currentPlayer.EncounteredError -= OnEncounteredError;
        _currentPlayer.TimeChanged -= OnTimeChanged;
        _currentPlayer.Buffering -= OnBuffering;

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
