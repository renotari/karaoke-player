using System;
using System.Reactive;
using ReactiveUI;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using LibVLCSharp.Shared;
using Avalonia.Media.Imaging;

namespace KaraokePlayer.ViewModels;

public class PlaybackWindowViewModel : ViewModelBase
{
    private readonly IMediaPlayerController? _mediaPlayerController;
    private readonly IAudioVisualizationEngine? _audioVisualizationEngine;
    
    private MediaFile? _currentSong;
    private bool _isPlaying;
    private double _currentTime;
    private double _duration;
    private bool _subtitlesEnabled = true;
    private bool _subtitlesVisible;
    private string _currentSubtitle = string.Empty;
    private bool _isFullscreen;
    private bool _isBuffering;
    private bool _hasMedia;
    private bool _isVideoContent;
    private bool _isAudioContent;
    private string _currentTitle = string.Empty;
    private string _currentArtist = string.Empty;
    private Bitmap? _currentArtwork;
    private string _visualizationStyle = "bars";

    // Design-time constructor
    public PlaybackWindowViewModel()
    {
        InitializeCommands();
        LoadSampleData();
    }

    // Runtime constructor with dependency injection
    public PlaybackWindowViewModel(
        IMediaPlayerController mediaPlayerController,
        IAudioVisualizationEngine? audioVisualizationEngine = null)
    {
        _mediaPlayerController = mediaPlayerController ?? throw new ArgumentNullException(nameof(mediaPlayerController));
        _audioVisualizationEngine = audioVisualizationEngine;

        InitializeCommands();
        SubscribeToServiceEvents();
    }

    private void InitializeCommands()
    {
        ToggleFullscreenCommand = ReactiveCommand.Create(ToggleFullscreen);
        ToggleSubtitlesCommand = ReactiveCommand.Create(ToggleSubtitles);
    }

    private void SubscribeToServiceEvents()
    {
        if (_mediaPlayerController != null)
        {
            _mediaPlayerController.StateChanged += OnPlaybackStateChanged;
            _mediaPlayerController.TimeChanged += OnPlaybackTimeChanged;
            _mediaPlayerController.MediaEnded += OnMediaEnded;
            _mediaPlayerController.PlaybackError += OnPlaybackError;
        }
    }

    // Properties
    public MediaFile? CurrentSong
    {
        get => _currentSong;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentSong, value);
            UpdateMediaInfo();
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
    }

    public double CurrentTime
    {
        get => _currentTime;
        set => this.RaiseAndSetIfChanged(ref _currentTime, value);
    }

    public double Duration
    {
        get => _duration;
        set => this.RaiseAndSetIfChanged(ref _duration, value);
    }

    public bool SubtitlesEnabled
    {
        get => _subtitlesEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _subtitlesEnabled, value);
            UpdateSubtitlesVisibility();
        }
    }

    public bool SubtitlesVisible
    {
        get => _subtitlesVisible;
        set => this.RaiseAndSetIfChanged(ref _subtitlesVisible, value);
    }

    public string CurrentSubtitle
    {
        get => _currentSubtitle;
        set => this.RaiseAndSetIfChanged(ref _currentSubtitle, value);
    }

    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => this.RaiseAndSetIfChanged(ref _isFullscreen, value);
    }

    public bool IsBuffering
    {
        get => _isBuffering;
        set => this.RaiseAndSetIfChanged(ref _isBuffering, value);
    }

    public bool HasMedia
    {
        get => _hasMedia;
        set => this.RaiseAndSetIfChanged(ref _hasMedia, value);
    }

    public bool IsVideoContent
    {
        get => _isVideoContent;
        set => this.RaiseAndSetIfChanged(ref _isVideoContent, value);
    }

    public bool IsAudioContent
    {
        get => _isAudioContent;
        set => this.RaiseAndSetIfChanged(ref _isAudioContent, value);
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        set => this.RaiseAndSetIfChanged(ref _currentTitle, value);
    }

    public string CurrentArtist
    {
        get => _currentArtist;
        set => this.RaiseAndSetIfChanged(ref _currentArtist, value);
    }

    public Bitmap? CurrentArtwork
    {
        get => _currentArtwork;
        set => this.RaiseAndSetIfChanged(ref _currentArtwork, value);
    }

    public string VisualizationStyle
    {
        get => _visualizationStyle;
        set => this.RaiseAndSetIfChanged(ref _visualizationStyle, value);
    }

    public MediaPlayer? MediaPlayer => _mediaPlayerController?.GetActiveMediaPlayer();

    // Commands
    public ReactiveCommand<Unit, Unit> ToggleFullscreenCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ToggleSubtitlesCommand { get; private set; } = null!;

    // Events
    public event EventHandler<bool>? FullscreenRequested;
    public event EventHandler? VisualizationUpdateRequested;

    // Command implementations
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        FullscreenRequested?.Invoke(this, IsFullscreen);
    }

    private void ToggleSubtitles()
    {
        SubtitlesEnabled = !SubtitlesEnabled;
        _mediaPlayerController?.ToggleSubtitles(SubtitlesEnabled);
    }

    // Service event handlers
    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == PlaybackState.Playing;
        IsBuffering = e.NewState == PlaybackState.Buffering;
        
        if (e.NewState == PlaybackState.Playing)
        {
            HasMedia = true;
        }
        else if (e.NewState == PlaybackState.Stopped)
        {
            HasMedia = false;
        }
    }

    private void OnPlaybackTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        CurrentTime = e.CurrentTime;
        Duration = e.Duration;

        // Request visualization update for audio content
        if (IsAudioContent)
        {
            VisualizationUpdateRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMediaEnded(object? sender, MediaEndedEventArgs e)
    {
        HasMedia = false;
        CurrentSong = null;
    }

    private void OnPlaybackError(object? sender, PlaybackErrorEventArgs e)
    {
        // Error handling - could show error message in UI
        HasMedia = false;
    }

    // Helper methods
    private void UpdateMediaInfo()
    {
        if (CurrentSong != null)
        {
            HasMedia = true;
            IsVideoContent = CurrentSong.Type == Models.MediaType.Video;
            IsAudioContent = CurrentSong.Type == Models.MediaType.Audio;

            CurrentTitle = CurrentSong.Metadata?.Title ?? CurrentSong.Filename;
            CurrentArtist = CurrentSong.Metadata?.Artist ?? string.Empty;

            // Load artwork for audio files
            if (IsAudioContent && !string.IsNullOrEmpty(CurrentSong.ThumbnailPath))
            {
                try
                {
                    CurrentArtwork = new Bitmap(CurrentSong.ThumbnailPath);
                }
                catch
                {
                    CurrentArtwork = null;
                }
            }
            else
            {
                CurrentArtwork = null;
            }

            UpdateSubtitlesVisibility();
        }
        else
        {
            HasMedia = false;
            IsVideoContent = false;
            IsAudioContent = false;
            CurrentTitle = string.Empty;
            CurrentArtist = string.Empty;
            CurrentArtwork = null;
            SubtitlesVisible = false;
        }
    }

    private void UpdateSubtitlesVisibility()
    {
        // Show subtitles only for video content when enabled
        SubtitlesVisible = IsVideoContent && SubtitlesEnabled && HasMedia;
    }

    public float[] GetAudioSpectrum()
    {
        // Get audio spectrum from media player controller
        return _mediaPlayerController?.GetAudioSpectrum() ?? Array.Empty<float>();
    }

    // Message bus methods for cross-window communication
    public void SendPlayPauseCommand()
    {
        // Send play/pause command via message bus
        ReactiveUI.MessageBus.Current.SendMessage(new PlaybackControlMessage { Action = "PlayPause" });
    }

    public void SendNextCommand()
    {
        ReactiveUI.MessageBus.Current.SendMessage(new PlaybackControlMessage { Action = "Next" });
    }

    public void SendPreviousCommand()
    {
        ReactiveUI.MessageBus.Current.SendMessage(new PlaybackControlMessage { Action = "Previous" });
    }

    public void SendVolumeUpCommand()
    {
        ReactiveUI.MessageBus.Current.SendMessage(new PlaybackControlMessage { Action = "VolumeUp" });
    }

    public void SendVolumeDownCommand()
    {
        ReactiveUI.MessageBus.Current.SendMessage(new PlaybackControlMessage { Action = "VolumeDown" });
    }

    public void SendMuteCommand()
    {
        ReactiveUI.MessageBus.Current.SendMessage(new PlaybackControlMessage { Action = "Mute" });
    }

    private void LoadSampleData()
    {
        // Sample data for design-time preview
        CurrentTitle = "Sample Song Title";
        CurrentArtist = "Sample Artist";
        HasMedia = true;
        IsAudioContent = true;
        Duration = 245;
        CurrentTime = 60;
    }
}

// Message class for cross-window communication
public class PlaybackControlMessage
{
    public string Action { get; set; } = string.Empty;
}
