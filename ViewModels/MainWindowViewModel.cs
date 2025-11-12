using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISearchEngine? _searchEngine;
    private readonly IPlaylistManager? _playlistManager;
    private readonly IMediaPlayerController? _mediaPlayerController;
    private readonly IMediaLibraryManager? _mediaLibraryManager;
    private readonly INotificationService? _notificationService;

    private string _searchQuery = string.Empty;
    private MediaFile? _selectedMediaFile;
    private PlaylistItemViewModel? _selectedPlaylistItem;
    private bool _isPlaying;
    private MediaFile? _currentSong;
    private double _currentTime;
    private double _duration;
    private int _volume = 75;
    private string _currentSongInfo = "No song playing";
    private string _statusMessage = "Ready";
    private bool _shuffleEnabled;
    private bool _crossfadeEnabled;
    private int _mediaLibraryCount;
    private bool _isVideoMode;
    private bool _isControlHandleExpanded;
    private string _videoModeSearchQuery = string.Empty;
    private System.Timers.Timer? _handleCollapseTimer;

    // Design-time constructor for XAML preview
    public MainWindowViewModel()
    {
        // Initialize collections
        MediaFiles = new ObservableCollection<MediaFile>();
        FilteredMediaFiles = new ObservableCollection<MediaFile>();
        CurrentPlaylist = new ObservableCollection<PlaylistItemViewModel>();

        // Setup reactive property for search filtering
        this.WhenAnyValue(x => x.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async query => await PerformSearchAsync(query));

        // Initialize commands
        InitializeCommands();

        // Load sample data for design-time preview
        LoadSampleData();
    }

    // Runtime constructor with dependency injection
    public MainWindowViewModel(
        ISearchEngine searchEngine,
        IPlaylistManager playlistManager,
        IMediaPlayerController mediaPlayerController,
        IMediaLibraryManager mediaLibraryManager,
        INotificationService notificationService)
    {
        _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
        _playlistManager = playlistManager ?? throw new ArgumentNullException(nameof(playlistManager));
        _mediaPlayerController = mediaPlayerController ?? throw new ArgumentNullException(nameof(mediaPlayerController));
        _mediaLibraryManager = mediaLibraryManager ?? throw new ArgumentNullException(nameof(mediaLibraryManager));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        // Initialize collections
        MediaFiles = new ObservableCollection<MediaFile>();
        FilteredMediaFiles = new ObservableCollection<MediaFile>();
        CurrentPlaylist = new ObservableCollection<PlaylistItemViewModel>();

        // Setup reactive property for search filtering
        this.WhenAnyValue(x => x.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async query => await PerformSearchAsync(query));

        // Initialize commands
        InitializeCommands();

        // Subscribe to service events
        SubscribeToServiceEvents();

        // Initialize data
        _ = InitializeAsync();
    }

    private void InitializeCommands()
    {
        // Search commands
        ClearSearchCommand = ReactiveCommand.Create(ClearSearch);
        SearchCommand = ReactiveCommand.CreateFromTask<string>(async query => await PerformSearchAsync(query));

        // Playlist commands
        AddToPlaylistNextCommand = ReactiveCommand.CreateFromTask<MediaFile>(async file => await AddToPlaylistAsync(file, "next"));
        AddToPlaylistEndCommand = ReactiveCommand.CreateFromTask<MediaFile>(async file => await AddToPlaylistAsync(file, "end"));
        RemoveSongCommand = ReactiveCommand.CreateFromTask<PlaylistItemViewModel>(RemoveSongAsync);
        
        // Commands with observable conditions
        var canClearPlaylist = this.WhenAnyValue(
            x => x.CurrentPlaylist.Count,
            count => count > 0);
        ClearPlaylistCommand = ReactiveCommand.CreateFromTask(ClearPlaylistAsync, canClearPlaylist);
        
        var canShufflePlaylist = this.WhenAnyValue(
            x => x.CurrentPlaylist.Count,
            count => count > 1);
        ShufflePlaylistCommand = ReactiveCommand.CreateFromTask(ShufflePlaylistAsync, canShufflePlaylist);

        // Playback commands
        var canPlay = this.WhenAnyValue(
            x => x.CurrentPlaylist.Count,
            count => count > 0);
        PlayCommand = ReactiveCommand.CreateFromTask(PlayAsync, canPlay);
        PauseCommand = ReactiveCommand.Create(Pause);
        PlayPauseCommand = ReactiveCommand.Create(PlayPause);
        StopCommand = ReactiveCommand.Create(Stop);
        NextCommand = ReactiveCommand.Create(Next);
        PreviousCommand = ReactiveCommand.Create(Previous);

        // Video Mode commands
        ToggleVideoModeCommand = ReactiveCommand.Create(ToggleVideoMode);
        ExpandControlHandleCommand = ReactiveCommand.Create(ExpandControlHandle);
        CollapseControlHandleCommand = ReactiveCommand.Create(CollapseControlHandle);
        AddToPlaylistFromVideoModeCommand = ReactiveCommand.CreateFromTask<MediaFile>(async file => await AddToPlaylistAsync(file, "next"));

        // Menu commands
        OpenMediaDirectoryCommand = ReactiveCommand.CreateFromTask(OpenMediaDirectoryAsync);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
        ShowAboutCommand = ReactiveCommand.Create(ShowAbout);
        ExitCommand = ReactiveCommand.Create(Exit);

        // Initialize collapse timer
        _handleCollapseTimer = new System.Timers.Timer(3000); // 3 seconds
        _handleCollapseTimer.Elapsed += (s, e) => CollapseControlHandle();
        _handleCollapseTimer.AutoReset = false;
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

        if (_playlistManager != null)
        {
            _playlistManager.PlaylistChanged += OnPlaylistChanged;
        }

        if (_mediaLibraryManager != null)
        {
            _mediaLibraryManager.FilesAdded += OnFilesAdded;
            _mediaLibraryManager.FilesRemoved += OnFilesRemoved;
            _mediaLibraryManager.FilesModified += OnFilesModified;
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Load media library
            if (_mediaLibraryManager != null)
            {
                var files = await _mediaLibraryManager.GetMediaFilesAsync();
                foreach (var file in files)
                {
                    MediaFiles.Add(file);
                    FilteredMediaFiles.Add(file);
                }
                MediaLibraryCount = files.Count;
            }

            // Restore last session playlist
            if (_playlistManager != null)
            {
                await _playlistManager.RestoreLastSessionAsync();
                SyncPlaylistFromService();
            }

            StatusMessage = $"Ready - {MediaLibraryCount} songs in library";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error initializing: {ex.Message}";
        }
    }

    // Properties
    public string SearchQuery
    {
        get => _searchQuery;
        set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
    }

    public MediaFile? SelectedMediaFile
    {
        get => _selectedMediaFile;
        set => this.RaiseAndSetIfChanged(ref _selectedMediaFile, value);
    }

    public PlaylistItemViewModel? SelectedPlaylistItem
    {
        get => _selectedPlaylistItem;
        set => this.RaiseAndSetIfChanged(ref _selectedPlaylistItem, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
    }

    public MediaFile? CurrentSong
    {
        get => _currentSong;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentSong, value);
            UpdateCurrentSongInfo();
        }
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

    public int Volume
    {
        get => _volume;
        set
        {
            this.RaiseAndSetIfChanged(ref _volume, value);
            _mediaPlayerController?.SetVolume(value / 100f);
        }
    }

    public string CurrentSongInfo
    {
        get => _currentSongInfo;
        set => this.RaiseAndSetIfChanged(ref _currentSongInfo, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool ShuffleEnabled
    {
        get => _shuffleEnabled;
        set => this.RaiseAndSetIfChanged(ref _shuffleEnabled, value);
    }

    public bool CrossfadeEnabled
    {
        get => _crossfadeEnabled;
        set => this.RaiseAndSetIfChanged(ref _crossfadeEnabled, value);
    }

    public int MediaLibraryCount
    {
        get => _mediaLibraryCount;
        set => this.RaiseAndSetIfChanged(ref _mediaLibraryCount, value);
    }

    public bool IsVideoMode
    {
        get => _isVideoMode;
        set => this.RaiseAndSetIfChanged(ref _isVideoMode, value);
    }

    public bool IsControlHandleExpanded
    {
        get => _isControlHandleExpanded;
        set => this.RaiseAndSetIfChanged(ref _isControlHandleExpanded, value);
    }

    public string VideoModeSearchQuery
    {
        get => _videoModeSearchQuery;
        set => this.RaiseAndSetIfChanged(ref _videoModeSearchQuery, value);
    }

    // Collections
    public ObservableCollection<MediaFile> MediaFiles { get; }
    public ObservableCollection<MediaFile> FilteredMediaFiles { get; }
    public ObservableCollection<PlaylistItemViewModel> CurrentPlaylist { get; }

    // Services (exposed for binding)
    public INotificationService? NotificationService => _notificationService;

    // Commands
    public ReactiveCommand<Unit, Unit> ClearSearchCommand { get; private set; } = null!;
    public ReactiveCommand<string, Unit> SearchCommand { get; private set; } = null!;
    public ReactiveCommand<MediaFile, Unit> AddToPlaylistNextCommand { get; private set; } = null!;
    public ReactiveCommand<MediaFile, Unit> AddToPlaylistEndCommand { get; private set; } = null!;
    public ReactiveCommand<PlaylistItemViewModel, Unit> RemoveSongCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ClearPlaylistCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShufflePlaylistCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> PlayCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> PauseCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> StopCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> NextCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> PreviousCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ToggleVideoModeCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExpandControlHandleCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CollapseControlHandleCommand { get; private set; } = null!;
    public ReactiveCommand<MediaFile, Unit> AddToPlaylistFromVideoModeCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> OpenMediaDirectoryCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExitCommand { get; private set; } = null!;

    // Command implementations
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    private async Task PerformSearchAsync(string? query)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            FilteredMediaFiles.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                // Show all files when search is empty
                foreach (var file in MediaFiles)
                {
                    FilteredMediaFiles.Add(file);
                }
                return;
            }

            // Use search engine if available, otherwise fallback to local filtering
            if (_searchEngine != null)
            {
                var results = await _searchEngine.SearchAsync(query);
                foreach (var file in results)
                {
                    FilteredMediaFiles.Add(file);
                }

                // Add to search history
                if (!string.IsNullOrWhiteSpace(query))
                {
                    await _searchEngine.AddToHistoryAsync(query);
                }
            }
            else
            {
                // Fallback: local filtering
                var lowerQuery = query.ToLower();
                var filtered = MediaFiles.Where(f =>
                    (f.Metadata?.Artist?.ToLower().Contains(lowerQuery) ?? false) ||
                    (f.Metadata?.Title?.ToLower().Contains(lowerQuery) ?? false) ||
                    f.Filename.ToLower().Contains(lowerQuery)
                );

                foreach (var file in filtered)
                {
                    FilteredMediaFiles.Add(file);
                }
            }

            // Log search performance
            var duration = DateTime.UtcNow - startTime;
            if (duration.TotalMilliseconds > 300)
            {
                StatusMessage = $"Search completed in {duration.TotalMilliseconds:F0}ms (slow)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
    }

    private async Task AddToPlaylistAsync(MediaFile mediaFile, string position)
    {
        try
        {
            if (_playlistManager != null)
            {
                // Optimistic update: Add to UI immediately for < 50ms response
                var isDuplicate = _playlistManager.IsDuplicate(mediaFile);
                var playlistItem = new PlaylistItem
                {
                    MediaFileId = mediaFile.Id,
                    MediaFile = mediaFile,
                    Position = position == "next" && CurrentPlaylist.Count > 0 ? 1 : CurrentPlaylist.Count,
                    IsDuplicate = isDuplicate
                };

                var viewModel = new PlaylistItemViewModel(playlistItem, mediaFile);

                if (position == "next" && CurrentPlaylist.Count > 0)
                {
                    CurrentPlaylist.Insert(1, viewModel);
                }
                else
                {
                    CurrentPlaylist.Add(viewModel);
                }

                // Update service in background
                await _playlistManager.AddSongAsync(mediaFile, position);

                StatusMessage = $"Added '{mediaFile.Metadata?.Title ?? mediaFile.Filename}' to playlist ({position})";
            }
            else
            {
                // Fallback without service
                var playlistItem = new PlaylistItem
                {
                    MediaFileId = mediaFile.Id,
                    MediaFile = mediaFile,
                    Position = position == "next" && CurrentPlaylist.Count > 0 ? 1 : CurrentPlaylist.Count,
                    IsDuplicate = CurrentPlaylist.Any(p => p.PlaylistItem.MediaFileId == mediaFile.Id)
                };

                var viewModel = new PlaylistItemViewModel(playlistItem, mediaFile);

                if (position == "next" && CurrentPlaylist.Count > 0)
                {
                    CurrentPlaylist.Insert(1, viewModel);
                }
                else
                {
                    CurrentPlaylist.Add(viewModel);
                }

                StatusMessage = $"Added '{mediaFile.Metadata?.Title ?? mediaFile.Filename}' to playlist ({position})";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding to playlist: {ex.Message}";
        }
    }

    private async Task RemoveSongAsync(PlaylistItemViewModel item)
    {
        try
        {
            var index = CurrentPlaylist.IndexOf(item);
            if (index >= 0)
            {
                // Optimistic update: Remove from UI immediately
                CurrentPlaylist.RemoveAt(index);

                // Update service in background
                if (_playlistManager != null)
                {
                    await _playlistManager.RemoveSongAsync(index);
                }

                StatusMessage = "Song removed from playlist";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing song: {ex.Message}";
        }
    }

    private async Task ClearPlaylistAsync()
    {
        try
        {
            // Optimistic update: Clear UI immediately
            CurrentPlaylist.Clear();

            // Update service in background
            if (_playlistManager != null)
            {
                await _playlistManager.ClearPlaylistAsync();
            }

            StatusMessage = "Playlist cleared";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing playlist: {ex.Message}";
        }
    }

    private async Task ShufflePlaylistAsync()
    {
        try
        {
            if (_playlistManager != null)
            {
                await _playlistManager.ShufflePlaylistAsync();
                SyncPlaylistFromService();
            }
            else
            {
                // Fallback: local shuffle
                var random = new Random();
                var shuffled = CurrentPlaylist.OrderBy(x => random.Next()).ToList();
                CurrentPlaylist.Clear();
                for (int i = 0; i < shuffled.Count; i++)
                {
                    shuffled[i].PlaylistItem.Position = i;
                    CurrentPlaylist.Add(shuffled[i]);
                }
            }

            StatusMessage = "Playlist shuffled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error shuffling playlist: {ex.Message}";
        }
    }

    private async Task PlayAsync()
    {
        try
        {
            if (_mediaPlayerController != null && CurrentPlaylist.Count > 0)
            {
                var firstSong = CurrentPlaylist[0].MediaFile;
                if (firstSong != null)
                {
                    await _mediaPlayerController.PlayAsync(firstSong);
                    CurrentSong = firstSong;
                    IsPlaying = true;

                    // Mark as currently playing
                    CurrentPlaylist[0].IsCurrentlyPlaying = true;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Playback error: {ex.Message}";
        }
    }

    private void Pause()
    {
        try
        {
            if (_mediaPlayerController != null)
            {
                if (IsPlaying)
                {
                    _mediaPlayerController.Pause();
                    IsPlaying = false;
                    StatusMessage = "Paused";
                }
                else
                {
                    _mediaPlayerController.Resume();
                    IsPlaying = true;
                    StatusMessage = "Playing";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void PlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            _ = PlayAsync();
        }
    }

    private void Stop()
    {
        try
        {
            _mediaPlayerController?.Stop();
            IsPlaying = false;
            CurrentTime = 0;
            CurrentSong = null;

            // Clear currently playing indicator
            foreach (var item in CurrentPlaylist)
            {
                item.IsCurrentlyPlaying = false;
            }

            StatusMessage = "Stopped";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void Next()
    {
        try
        {
            if (CurrentPlaylist.Count > 1)
            {
                // Remove current song and play next
                var currentIndex = CurrentPlaylist.IndexOf(CurrentPlaylist.FirstOrDefault(p => p.IsCurrentlyPlaying) ?? CurrentPlaylist[0]);
                if (currentIndex >= 0 && currentIndex < CurrentPlaylist.Count - 1)
                {
                    CurrentPlaylist[currentIndex].IsCurrentlyPlaying = false;
                    var nextSong = CurrentPlaylist[currentIndex + 1];
                    if (nextSong.MediaFile != null && _mediaPlayerController != null)
                    {
                        _ = _mediaPlayerController.PlayAsync(nextSong.MediaFile);
                        CurrentSong = nextSong.MediaFile;
                        nextSong.IsCurrentlyPlaying = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void Previous()
    {
        try
        {
            var currentIndex = CurrentPlaylist.IndexOf(CurrentPlaylist.FirstOrDefault(p => p.IsCurrentlyPlaying) ?? CurrentPlaylist[0]);
            if (currentIndex > 0)
            {
                CurrentPlaylist[currentIndex].IsCurrentlyPlaying = false;
                var previousSong = CurrentPlaylist[currentIndex - 1];
                if (previousSong.MediaFile != null && _mediaPlayerController != null)
                {
                    _ = _mediaPlayerController.PlayAsync(previousSong.MediaFile);
                    CurrentSong = previousSong.MediaFile;
                    previousSong.IsCurrentlyPlaying = true;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    // Event handlers for service events
    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == PlaybackState.Playing;
        
        if (e.NewState == PlaybackState.Error)
        {
            StatusMessage = "Playback error occurred";
        }
    }

    private void OnPlaybackTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        CurrentTime = e.CurrentTime;
        Duration = e.Duration;
    }

    private void OnMediaEnded(object? sender, MediaEndedEventArgs e)
    {
        // Auto-play next song
        Next();
    }

    private void OnPlaybackError(object? sender, PlaybackErrorEventArgs e)
    {
        StatusMessage = $"Playback error: {e.ErrorMessage}";
        
        // Mark the problematic file in playlist
        var errorItem = CurrentPlaylist.FirstOrDefault(p => p.MediaFile?.Id == e.MediaFile?.Id);
        if (errorItem != null)
        {
            errorItem.PlaylistItem.Error = e.ErrorMessage;
        }

        // Skip to next song
        Next();
    }

    private void OnPlaylistChanged(object? sender, PlaylistChangedEventArgs e)
    {
        // Sync playlist from service when it changes externally
        SyncPlaylistFromService();
    }

    private void OnFilesAdded(object? sender, MediaFilesChangedEventArgs e)
    {
        foreach (var file in e.Files)
        {
            MediaFiles.Add(file);
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredMediaFiles.Add(file);
            }
        }
        MediaLibraryCount = MediaFiles.Count;
        StatusMessage = $"{e.Files.Count} new songs added to library";
    }

    private void OnFilesRemoved(object? sender, MediaFilesChangedEventArgs e)
    {
        foreach (var file in e.Files)
        {
            var existing = MediaFiles.FirstOrDefault(f => f.Id == file.Id);
            if (existing != null)
            {
                MediaFiles.Remove(existing);
                FilteredMediaFiles.Remove(existing);
            }
        }
        MediaLibraryCount = MediaFiles.Count;
        StatusMessage = $"{e.Files.Count} songs removed from library";
    }

    private void OnFilesModified(object? sender, MediaFilesChangedEventArgs e)
    {
        foreach (var file in e.Files)
        {
            var existing = MediaFiles.FirstOrDefault(f => f.Id == file.Id);
            if (existing != null)
            {
                var index = MediaFiles.IndexOf(existing);
                MediaFiles[index] = file;

                var filteredIndex = FilteredMediaFiles.IndexOf(existing);
                if (filteredIndex >= 0)
                {
                    FilteredMediaFiles[filteredIndex] = file;
                }
            }
        }
        StatusMessage = $"{e.Files.Count} songs updated";
    }

    private void SyncPlaylistFromService()
    {
        if (_playlistManager == null) return;

        CurrentPlaylist.Clear();
        var servicePlaylist = _playlistManager.GetCurrentPlaylist();
        foreach (var item in servicePlaylist)
        {
            if (item.MediaFile != null)
            {
                CurrentPlaylist.Add(new PlaylistItemViewModel(item, item.MediaFile));
            }
        }
    }

    private void UpdateCurrentSongInfo()
    {
        if (CurrentSong != null)
        {
            var artist = CurrentSong.Metadata?.Artist ?? "Unknown Artist";
            var title = CurrentSong.Metadata?.Title ?? CurrentSong.Filename;
            CurrentSongInfo = $"{artist} - {title}";
        }
        else
        {
            CurrentSongInfo = "No song playing";
        }
    }

    private void ToggleVideoMode()
    {
        IsVideoMode = !IsVideoMode;
        
        if (!IsVideoMode)
        {
            // Collapse handle when exiting video mode
            IsControlHandleExpanded = false;
            _handleCollapseTimer?.Stop();
        }
        
        StatusMessage = IsVideoMode ? "Video Mode enabled" : "Normal Mode enabled";
    }

    private void ExpandControlHandle()
    {
        if (!IsVideoMode) return;

        IsControlHandleExpanded = true;
        
        // Reset the auto-collapse timer
        _handleCollapseTimer?.Stop();
        _handleCollapseTimer?.Start();
    }

    private void CollapseControlHandle()
    {
        if (!IsVideoMode) return;

        // Use dispatcher to ensure UI thread access
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsControlHandleExpanded = false;
            _handleCollapseTimer?.Stop();
        });
    }

    private void LoadSampleData()
    {
        // Sample data for design-time preview
        var sampleFiles = new[]
        {
            new MediaFile
            {
                Id = "1",
                Filename = "Song1.mp4",
                FilePath = "/path/to/song1.mp4",
                Type = MediaType.Video,
                Format = MediaFormat.MP4,
                MetadataLoaded = true,
                ThumbnailLoaded = false,
                Metadata = new MediaMetadata
                {
                    Artist = "Artist One",
                    Title = "Amazing Song",
                    Duration = 245
                }
            },
            new MediaFile
            {
                Id = "2",
                Filename = "Song2.mp3",
                FilePath = "/path/to/song2.mp3",
                Type = MediaType.Audio,
                Format = MediaFormat.MP3,
                MetadataLoaded = true,
                ThumbnailLoaded = false,
                Metadata = new MediaMetadata
                {
                    Artist = "Artist Two",
                    Title = "Beautiful Melody",
                    Duration = 198
                }
            }
        };

        foreach (var file in sampleFiles)
        {
            MediaFiles.Add(file);
            FilteredMediaFiles.Add(file);
        }

        MediaLibraryCount = MediaFiles.Count;
        Duration = 245;
    }

    // Keyboard shortcut methods
    public void VolumeUp()
    {
        Volume = Math.Min(100, Volume + 5);
    }

    public void VolumeDown()
    {
        Volume = Math.Max(0, Volume - 5);
    }

    private bool _isMuted;
    private int _volumeBeforeMute;

    public void ToggleMute()
    {
        if (_isMuted)
        {
            Volume = _volumeBeforeMute;
            _isMuted = false;
            StatusMessage = "Unmuted";
        }
        else
        {
            _volumeBeforeMute = Volume;
            Volume = 0;
            _isMuted = true;
            StatusMessage = "Muted";
        }
    }

    public void ToggleFullscreen()
    {
        // This will be handled by the window itself
        // For now, just toggle video mode as a placeholder
        ToggleVideoMode();
    }

    public void AddSelectedToPlaylistEnd()
    {
        if (SelectedMediaFile != null)
        {
            _ = AddToPlaylistAsync(SelectedMediaFile, "end");
        }
    }

    public void AddSelectedToPlaylistNext()
    {
        if (SelectedMediaFile != null)
        {
            _ = AddToPlaylistAsync(SelectedMediaFile, "next");
        }
    }

    public void RemoveSelectedFromPlaylist()
    {
        if (SelectedPlaylistItem != null)
        {
            _ = RemoveSongAsync(SelectedPlaylistItem);
        }
    }

    public void FocusSearch()
    {
        // This will be handled by the view
        // Raise an event or use a message bus to notify the view
        StatusMessage = "Search focused";
    }

    public async void OpenPlaylistComposer()
    {
        // This will open the playlist composer window
        StatusMessage = "Opening Playlist Composer...";
        
        // Use ReactiveUI MessageBus to request window opening
        ReactiveUI.MessageBus.Current.SendMessage(new OpenPlaylistComposerMessage());
    }

    public async void OpenSettings()
    {
        // This will open the settings window
        StatusMessage = "Opening Settings...";
        
        // Use ReactiveUI MessageBus to request window opening
        ReactiveUI.MessageBus.Current.SendMessage(new Services.OpenSettingsMessage());
    }

    public void RefreshLibrary()
    {
        _ = InitializeAsync();
        StatusMessage = "Refreshing library...";
    }

    public void ToggleDisplayMode()
    {
        // Toggle between single and dual screen mode
        StatusMessage = "Display mode toggle not yet implemented";
        // TODO: Implement display mode toggle
    }

    public void CloseDialog()
    {
        // Close any open dialogs or exit fullscreen
        if (IsVideoMode)
        {
            ToggleVideoMode();
        }
        StatusMessage = "Dialog closed";
    }

    public async Task OpenMediaDirectoryAsync()
    {
        // Use ReactiveUI MessageBus to request folder picker
        ReactiveUI.MessageBus.Current.SendMessage(new OpenMediaDirectoryMessage());
    }

    public void ShowAbout()
    {
        // Use ReactiveUI MessageBus to request about dialog
        ReactiveUI.MessageBus.Current.SendMessage(new ShowAboutMessage());
    }

    public void Exit()
    {
        // Use ReactiveUI MessageBus to request application exit
        ReactiveUI.MessageBus.Current.SendMessage(new ExitApplicationMessage());
    }

    public void Dispose()
    {
        _handleCollapseTimer?.Dispose();
    }

    /// <summary>
    /// Gets the playlist manager for use by child windows
    /// </summary>
    public IPlaylistManager? GetPlaylistManager()
    {
        return _playlistManager;
    }
}

// Message classes for inter-component communication
public class OpenMediaDirectoryMessage { }
public class ShowAboutMessage { }
public class ExitApplicationMessage { }
