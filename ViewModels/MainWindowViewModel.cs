using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI; // Keep for throttled search and RxApp.MainThreadScheduler
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ISearchEngine? _searchEngine;
    private readonly IPlaylistManager? _playlistManager;
    private readonly IMediaPlayerController? _mediaPlayerController;
    private readonly IMediaLibraryManager? _mediaLibraryManager;
    private readonly INotificationService? _notificationService;
    private readonly IMetadataExtractor? _metadataExtractor;
    private readonly IThumbnailGenerator? _thumbnailGenerator;
    private readonly CompositeDisposable _disposables = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;
    
    [ObservableProperty]
    private MediaFile? _selectedMediaFile;
    
    [ObservableProperty]
    private PlaylistItemViewModel? _selectedPlaylistItem;
    
    [ObservableProperty]
    private bool _isPlaying;
    
    [ObservableProperty]
    private MediaFile? _currentSong;
    
    [ObservableProperty]
    private double _currentTime;
    
    [ObservableProperty]
    private double _duration;
    
    [ObservableProperty]
    private int _volume = 75;
    
    [ObservableProperty]
    private string _currentSongInfo = "No song playing";
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    [ObservableProperty]
    private bool _shuffleEnabled;
    
    [ObservableProperty]
    private bool _crossfadeEnabled;
    
    [ObservableProperty]
    private int _mediaLibraryCount;
    
    [ObservableProperty]
    private bool _isVideoMode;
    
    [ObservableProperty]
    private bool _isControlHandleExpanded;
    
    [ObservableProperty]
    private string _videoModeSearchQuery = string.Empty;
    
    private System.Timers.Timer? _handleCollapseTimer;

    // Design-time constructor for XAML preview
    public MainWindowViewModel()
    {
        // Initialize collections
        MediaFiles = new ObservableCollection<MediaFile>();
        CurrentPlaylist = new ObservableCollection<PlaylistItemViewModel>();

        // Setup reactive property for throttled search (KEEP ReactiveUI for this)
        var throttledSearch = this.WhenAnyValue(x => x.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(UiScheduler)
            .Subscribe(async query => await Search(query));
        _disposables.Add(throttledSearch);

        // Initialize commands (auto-generated)
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
        INotificationService notificationService,
        IMetadataExtractor? metadataExtractor = null,
        IThumbnailGenerator? thumbnailGenerator = null)
    {
        _metadataExtractor = metadataExtractor;
        _thumbnailGenerator = thumbnailGenerator;
        _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
        _playlistManager = playlistManager ?? throw new ArgumentNullException(nameof(playlistManager));
        _mediaPlayerController = mediaPlayerController ?? throw new ArgumentNullException(nameof(mediaPlayerController));
        _mediaLibraryManager = mediaLibraryManager ?? throw new ArgumentNullException(nameof(mediaLibraryManager));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        // Initialize collections
        MediaFiles = new ObservableCollection<MediaFile>();
        CurrentPlaylist = new ObservableCollection<PlaylistItemViewModel>();

        // Setup reactive property for throttled search (KEEP ReactiveUI for this)
        var throttledSearch = this.WhenAnyValue(x => x.SearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(UiScheduler)
            .Subscribe(async query => await Search(query));
        _disposables.Add(throttledSearch);

        // Initialize commands (auto-generated)
        InitializeCommands();

        // Subscribe to service events
        SubscribeToServiceEvents();

        // Initialize data
        _ = InitializeAsync();
    }

    private void InitializeCommands()
    {
        // Commands are auto-generated via RelayCommand attributes
        
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
            _mediaPlayerController.CrossfadeStarted += OnCrossfadeStarted;
        }

        if (_playlistManager != null)
        {
            _playlistManager.PlaylistChanged += OnPlaylistChanged;
        }

        if (_metadataExtractor != null)
        {
            _metadataExtractor.MetadataExtracted += OnMetadataExtracted;
        }

        if (_thumbnailGenerator != null)
        {
            _thumbnailGenerator.ThumbnailGenerated += OnThumbnailGenerated;
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
                await LoadLibraryAsync();
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

    // Partial methods for properties with additional logic
    partial void OnCurrentSongChanged(MediaFile? value)
    {
        UpdateCurrentSongInfo();
        OnPropertyChanged(nameof(IsVideoContent));
        OnPropertyChanged(nameof(IsAudioContent));
        OnPropertyChanged(nameof(IsIdle));
        UpdateActiveVideoSurface();
        OnPropertyChanged(nameof(IsVideoSurfaceAVisible));
        OnPropertyChanged(nameof(IsVideoSurfaceBVisible));
        OnPropertyChanged(nameof(IsVideoSurfaceAActive));
        OnPropertyChanged(nameof(IsVideoSurfaceBActive));
        OnPropertyChanged(nameof(NextSongInfo));
        OnPropertyChanged(nameof(HasNextSong));
    }

    /// <summary>True while a video file is the current song (the video surface is shown).</summary>
    public bool IsVideoContent => CurrentSong?.Type == MediaType.Video;

    /// <summary>True while an audio file is the current song (the "now playing" panel is shown).</summary>
    public bool IsAudioContent => CurrentSong?.Type == MediaType.Audio;

    /// <summary>True when nothing is playing (the waiting screen is shown).</summary>
    public bool IsIdle => CurrentSong == null;

    /// <summary>Native windows the two players render video into (from the view).</summary>
    public void AttachVideoWindows(IntPtr first, IntPtr second) => _mediaPlayerController?.AttachVideoWindows(first, second);

    /// <summary>Which of the two video surfaces belongs to the player of the current song.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoSurfaceAVisible), nameof(IsVideoSurfaceBVisible),
        nameof(IsVideoSurfaceAActive), nameof(IsVideoSurfaceBActive))]
    private int _activeVideoSurface;

    /// <summary>Which surface shows the video, wherever the surfaces are (playback window in dual screen).</summary>
    public bool IsVideoSurfaceAActive => IsVideoContent && ActiveVideoSurface == 0;
    public bool IsVideoSurfaceBActive => IsVideoContent && ActiveVideoSurface == 1;

    /// <summary>Surfaces of the main window: hidden in dual screen (the video is on the playback window).</summary>
    public bool IsVideoSurfaceAVisible => !IsDualScreenMode && IsVideoSurfaceAActive;
    public bool IsVideoSurfaceBVisible => !IsDualScreenMode && IsVideoSurfaceBActive;

    private void UpdateActiveVideoSurface()
    {
        if (_mediaPlayerController != null)
            ActiveVideoSurface = _mediaPlayerController.ActivePlayerIndex;
    }

    partial void OnVolumeChanged(int value)
    {
        _mediaPlayerController?.SetVolume(value / 100f);
    }

    // Collections
    public ObservableCollection<MediaFile> MediaFiles { get; }
    /// <summary>
    /// What the catalog shows. Replaced as a whole (not item by item) so that a
    /// search over a large library does not raise thousands of change events.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MediaFile> _filteredMediaFiles = new();
    public ObservableCollection<PlaylistItemViewModel> CurrentPlaylist { get; }

    // Services (exposed for binding)
    public INotificationService? NotificationService => _notificationService;

    // Commands
    // Commands are auto-generated via RelayCommand attributes

    // Command implementations
    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private async Task Search(string? query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Show the whole library when the search is empty
                FilteredMediaFiles = new ObservableCollection<MediaFile>(MediaFiles);
                return;
            }

            var startTime = DateTime.UtcNow;

            if (_searchEngine != null)
            {
                var results = await _searchEngine.SearchAsync(query);

                // A newer query may have been typed while this one was running
                if (query != SearchQuery && query != VideoModeSearchQuery)
                    return;

                FilteredMediaFiles = new ObservableCollection<MediaFile>(results);
                await _searchEngine.AddToHistoryAsync(query);
            }
            else
            {
                // Fallback: local filtering
                var filtered = MediaFiles.Where(f =>
                    f.DisplayArtist.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    f.DisplayTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    f.Filename.Contains(query, StringComparison.CurrentCultureIgnoreCase));
                FilteredMediaFiles = new ObservableCollection<MediaFile>(filtered);
            }

            var duration = DateTime.UtcNow - startTime;
            StatusMessage = FilteredMediaFiles.Count == 0
                ? $"No songs found for '{query}'"
                : $"{FilteredMediaFiles.Count} songs found" + (duration.TotalMilliseconds > 300 ? $" ({duration.TotalMilliseconds:F0} ms)" : "");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
    }

    // ----- Library -------------------------------------------------------------

    /// <summary>Catalog order: artist, then title (case-insensitive).</summary>
    private static System.Collections.Generic.List<MediaFile> SortForDisplay(System.Collections.Generic.IEnumerable<MediaFile> files)
        => files
            .OrderBy(f => f.DisplayArtist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(f => f.DisplayTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private void SetLibrary(System.Collections.Generic.IEnumerable<MediaFile> files)
    {
        // Materialize first: callers often pass a query over MediaFiles itself
        var sorted = SortForDisplay(files);

        MediaFiles.Clear();
        foreach (var file in sorted)
            MediaFiles.Add(file);
        MediaLibraryCount = MediaFiles.Count;
        ApplyFilter();
    }

    /// <summary>Re-applies the current search to the (changed) library.</summary>
    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            FilteredMediaFiles = new ObservableCollection<MediaFile>(MediaFiles);
        else
            _ = Search(SearchQuery);
    }

    private async Task LoadLibraryAsync()
    {
        if (_mediaLibraryManager == null)
            return;

        var files = await _mediaLibraryManager.GetMediaFilesAsync();
        SetLibrary(files);
    }

    // ----- Playlist: karaoke queue ---------------------------------------------
    //
    // CurrentPlaylist[0] is the song playing (while CurrentSong != null); the rest
    // is the queue. A song that ends or is skipped leaves the list and goes to
    // _history (used by Previous). With nothing playing, item 0 is the next song.
    //
    // The PlaylistManager is the single source of truth: commands ask it for a
    // change and CurrentPlaylist is rebuilt from its PlaylistChanged event.

    private readonly System.Collections.Generic.List<MediaFile> _history = new();
    private const int MaxHistory = 100;

    /// <summary>When false, playback stops at the end of each song (setting AutoPlayEnabled).</summary>
    public bool AutoPlayEnabled { get; set; } = true;

    private bool IsSongLoaded => CurrentSong != null;

    private async Task AddToPlaylist(MediaFile? mediaFile, string position)
    {
        if (_playlistManager == null || mediaFile == null)
            return;

        try
        {
            var count = _playlistManager.GetCurrentPlaylist().Count;
            var index = position == "next"
                ? (IsSongLoaded ? Math.Min(1, count) : 0)
                : count;

            await _playlistManager.InsertSongAsync(mediaFile, index);
            StatusMessage = position == "next"
                ? $"'{mediaFile.DisplayName}' will play next"
                : $"Added '{mediaFile.DisplayName}' to the end of the playlist";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding to playlist: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddToPlaylistNext(MediaFile file) => await AddToPlaylist(file, "next");

    [RelayCommand]
    private async Task AddToPlaylistEnd(MediaFile file) => await AddToPlaylist(file, "end");

    [RelayCommand]
    private async Task AddToPlaylistFromVideoMode(MediaFile file) => await AddToPlaylist(file, "next");

    [RelayCommand]
    private async Task RemoveSong(PlaylistItemViewModel? item)
    {
        if (_playlistManager == null || item == null)
            return;

        try
        {
            var index = CurrentPlaylist.IndexOf(item);
            if (index < 0)
                return;

            if (index == 0 && IsSongLoaded)
            {
                // Removing the song that is playing = skip it
                await AdvanceAsync(playNext: true);
            }
            else
            {
                await _playlistManager.RemoveSongAsync(index);
            }

            StatusMessage = "Song removed from playlist";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing song: {ex.Message}";
        }
    }

    /// <summary>Moves a queued song right after the one playing.</summary>
    [RelayCommand]
    private async Task PlaySongNext(PlaylistItemViewModel? item)
    {
        var index = item == null ? -1 : CurrentPlaylist.IndexOf(item);
        var target = IsSongLoaded ? 1 : 0;
        if (_playlistManager == null || index < 0 || index == target || (IsSongLoaded && index == 0))
            return;

        await _playlistManager.ReorderSongAsync(index, target);
    }

    /// <summary>Stops the current song and plays this one right away.</summary>
    [RelayCommand]
    private async Task PlaySongNow(PlaylistItemViewModel? item)
    {
        var index = item == null ? -1 : CurrentPlaylist.IndexOf(item);
        if (_playlistManager == null || index < 0)
            return;

        if (index == 0)
        {
            if (!IsSongLoaded)
                await PlayTopAsync();
            return;
        }

        if (IsSongLoaded)
        {
            // The current song is done: drop it, the chosen one shifts by one
            AddToHistory(CurrentSong!);
            await _playlistManager.RemoveSongAsync(0);
            index--;
        }

        if (index > 0)
            await _playlistManager.ReorderSongAsync(index, 0);

        await PlayTopAsync();
    }

    [RelayCommand]
    private async Task MoveSongUp(PlaylistItemViewModel? item)
    {
        var index = item == null ? -1 : CurrentPlaylist.IndexOf(item);
        var first = IsSongLoaded ? 1 : 0; // the song playing stays on top
        if (_playlistManager == null || index <= first)
            return;

        await _playlistManager.ReorderSongAsync(index, index - 1);
    }

    [RelayCommand]
    private async Task MoveSongDown(PlaylistItemViewModel? item)
    {
        var index = item == null ? -1 : CurrentPlaylist.IndexOf(item);
        if (_playlistManager == null || index < 0 || index >= CurrentPlaylist.Count - 1 || (IsSongLoaded && index == 0))
            return;

        await _playlistManager.ReorderSongAsync(index, index + 1);
    }

    // CanExecute methods for commands
    private bool CanClearPlaylist() => CurrentPlaylist.Count > (IsSongLoaded ? 1 : 0);
    private bool CanShufflePlaylist() => CurrentPlaylist.Count - (IsSongLoaded ? 1 : 0) > 1;
    private bool CanPlay() => CurrentPlaylist.Count > 0 || IsSongLoaded;

    private void RefreshCommandStates()
    {
        ClearPlaylistCommand.NotifyCanExecuteChanged();
        ShufflePlaylistCommand.NotifyCanExecuteChanged();
        PlayCommand.NotifyCanExecuteChanged();
        UpdatePreload();
    }

    // ----- Crossfade / preload ------------------------------------------------

    private string? _preloadedId;

    /// <summary>
    /// Keeps the song after the current one loaded in the controller's second
    /// player, so a crossfade can start on time. Called whenever the queue or
    /// the playback state changes.
    /// </summary>
    private void UpdatePreload()
    {
        if (_mediaPlayerController == null || _playlistManager == null)
            return;

        var list = _playlistManager.GetCurrentPlaylist();
        var next = IsSongLoaded && AutoPlayEnabled && CrossfadeEnabled && list.Count > 1
            ? list[1].MediaFile
            : null;

        if (next == null)
        {
            if (_preloadedId != null)
            {
                _mediaPlayerController.ClearPreload();
                _preloadedId = null;
            }
            return;
        }

        if (next.Id == _preloadedId)
            return;

        _preloadedId = next.Id;
        _ = _mediaPlayerController.PreloadNextAsync(next);
    }

    partial void OnCrossfadeEnabledChanged(bool value) => UpdatePreload();

    private void OnCrossfadeStarted(object? sender, CrossfadeStartedEventArgs e)
        => OnUiThread(() => _ = HandleCrossfadeStartedAsync(e));

    /// <summary>
    /// The controller already started the next song: advance the queue without
    /// starting playback again.
    /// </summary>
    private async Task HandleCrossfadeStartedAsync(CrossfadeStartedEventArgs e)
    {
        if (_playlistManager == null)
            return;

        _preloadedId = null; // consumed by the controller

        var finished = CurrentSong;
        if (finished != null)
        {
            AddToHistory(finished);
            var list = _playlistManager.GetCurrentPlaylist();
            if (list.Count > 0 && list[0].MediaFileId == finished.Id)
                await _playlistManager.RemoveSongAsync(0);
        }

        var current = _playlistManager.GetCurrentPlaylist();
        var index = current.FindIndex(i => i.MediaFileId == e.To.Id);
        if (index > 0)
            await _playlistManager.ReorderSongAsync(index, 0);
        else if (index < 0)
            await _playlistManager.InsertSongAsync(e.To, 0);

        var top = _playlistManager.GetCurrentPlaylist().FirstOrDefault()?.MediaFile ?? e.To;
        CurrentTime = 0;
        Duration = top.Metadata?.Duration ?? 0;
        CurrentSong = top;
        MarkCurrentlyPlaying();
        await ApplyShuffleModeAsync();
        RefreshCommandStates();
    }

    /// <summary>Shuffle mode (setting): the song after the current one is picked at random.</summary>
    public bool ShuffleModeEnabled
    {
        get => ShuffleEnabled;
        set => ShuffleEnabled = value;
    }

    private async Task ApplyShuffleModeAsync()
    {
        if (!ShuffleEnabled || _playlistManager == null)
            return;

        var count = _playlistManager.GetCurrentPlaylist().Count;
        if (count <= 2)
            return;

        var pick = Random.Shared.Next(1, count);
        if (pick != 1)
            await _playlistManager.ReorderSongAsync(pick, 1);
    }

    /// <summary>Clears the queue (the song playing, if any, is kept).</summary>
    [RelayCommand(CanExecute = nameof(CanClearPlaylist))]
    private async Task ClearPlaylist()
    {
        if (_playlistManager == null)
            return;

        try
        {
            await _playlistManager.ClearPlaylistAsync(keepFirst: IsSongLoaded);
            StatusMessage = "Playlist cleared";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing playlist: {ex.Message}";
        }
    }

    /// <summary>Shuffles the queue (the song playing, if any, stays first).</summary>
    [RelayCommand(CanExecute = nameof(CanShufflePlaylist))]
    private async Task ShufflePlaylist()
    {
        if (_playlistManager == null)
            return;

        try
        {
            await _playlistManager.ShufflePlaylistAsync(startIndex: IsSongLoaded ? 1 : 0);
            StatusMessage = "Playlist shuffled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error shuffling playlist: {ex.Message}";
        }
    }

    // ----- Transport ---------------------------------------------------------

    /// <summary>Resumes a paused song, or starts the first song of the playlist.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task Play()
    {
        if (IsSongLoaded && !IsPlaying)
        {
            _mediaPlayerController?.Resume();
            StatusMessage = "Playing";
            return;
        }

        if (!IsSongLoaded)
            await PlayTopAsync();
    }

    [RelayCommand]
    private void Pause()
    {
        if (IsPlaying)
        {
            _mediaPlayerController?.Pause();
            StatusMessage = "Paused";
        }
    }

    [RelayCommand]
    private async Task PlayPause()
    {
        if (IsPlaying)
            Pause();
        else
            await Play();
    }

    /// <summary>Stops playback; the song stays on top of the playlist.</summary>
    [RelayCommand]
    private void Stop()
    {
        try
        {
            _mediaPlayerController?.Stop();
            IsPlaying = false;
            CurrentTime = 0;
            Duration = 0;
            CurrentSong = null;
            MarkCurrentlyPlaying();
            RefreshCommandStates();
            StatusMessage = "Stopped";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>Skips to the next song (the current one leaves the playlist).</summary>
    [RelayCommand]
    private async Task Next()
    {
        if (IsSongLoaded)
            await AdvanceAsync(playNext: true);
        else if (CurrentPlaylist.Count > 0)
            await PlayTopAsync();
    }

    /// <summary>
    /// Restarts the current song when it has been playing for a few seconds,
    /// otherwise plays the previous song again.
    /// </summary>
    [RelayCommand]
    private async Task Previous()
    {
        if (_playlistManager == null || _mediaPlayerController == null)
            return;

        if (IsSongLoaded && (CurrentTime > 3 || _history.Count == 0))
        {
            _mediaPlayerController.Seek(0);
            return;
        }

        if (_history.Count == 0)
            return;

        var previous = _history[^1];
        _history.RemoveAt(_history.Count - 1);

        // The current song (if any) is pushed down to position 1: it plays next
        await _playlistManager.InsertSongAsync(previous, 0);
        await PlayTopAsync();
    }

    /// <summary>Plays CurrentPlaylist[0].</summary>
    private async Task PlayTopAsync()
    {
        if (_mediaPlayerController == null || _playlistManager == null)
            return;

        var top = _playlistManager.GetCurrentPlaylist().FirstOrDefault()?.MediaFile;
        if (top == null)
        {
            GoIdle();
            return;
        }

        try
        {
            if (_preloadedId == top.Id)
                _preloadedId = null; // the controller drops the preload when it is played directly

            CurrentTime = 0;
            Duration = top.Metadata?.Duration ?? 0;
            CurrentSong = top;
            MarkCurrentlyPlaying();
            await _mediaPlayerController.PlayAsync(top);
            await ApplyShuffleModeAsync();
            RefreshCommandStates();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Playback error: {ex.Message}";
        }
    }

    /// <summary>
    /// The current song is finished (ended, skipped or failed): remove it from the
    /// playlist and, if requested, start the next one.
    /// </summary>
    private async Task AdvanceAsync(bool playNext)
    {
        if (_playlistManager == null)
            return;

        var finished = CurrentSong;
        if (finished != null)
        {
            AddToHistory(finished);
            var list = _playlistManager.GetCurrentPlaylist();
            if (list.Count > 0 && list[0].MediaFileId == finished.Id)
            {
                await _playlistManager.RemoveSongAsync(0);
            }
        }

        if (playNext && _playlistManager.GetCurrentPlaylist().Count > 0)
        {
            await PlayTopAsync();
        }
        else
        {
            _mediaPlayerController?.Stop();
            GoIdle();
        }
    }

    private void GoIdle()
    {
        CurrentSong = null;
        IsPlaying = false;
        CurrentTime = 0;
        Duration = 0;
        MarkCurrentlyPlaying();
        RefreshCommandStates();
        OnPropertyChanged(nameof(NextSongInfo));
        OnPropertyChanged(nameof(HasNextSong));
    }

    private void AddToHistory(MediaFile file)
    {
        _history.Add(file);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);
    }

    /// <summary>Flags CurrentPlaylist[0] as "now playing" while a song is loaded.</summary>
    private void MarkCurrentlyPlaying()
    {
        for (int i = 0; i < CurrentPlaylist.Count; i++)
        {
            CurrentPlaylist[i].IsCurrentlyPlaying =
                i == 0 && CurrentSong != null && CurrentPlaylist[i].MediaFile?.Id == CurrentSong.Id;
        }
    }

    // Event handlers for service events
    /// <summary>
    /// Service events (LibVLC, FileSystemWatcher, timers) arrive on background threads:
    /// bound properties and collections must only be touched on the UI thread.
    /// </summary>
    private static void OnUiThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            action();
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
        => OnUiThread(() => HandlePlaybackStateChanged(e));

    private void HandlePlaybackStateChanged(PlaybackStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == PlaybackState.Playing;

        
        if (e.NewState == PlaybackState.Error)
        {
            StatusMessage = "Playback error occurred";
        }
    }

    private void OnPlaybackTimeChanged(object? sender, TimeChangedEventArgs e)
        => OnUiThread(() => HandlePlaybackTimeChanged(e));

    private void HandlePlaybackTimeChanged(TimeChangedEventArgs e)
    {
        CurrentTime = e.CurrentTime;
        Duration = e.Duration;
    }

    private void OnMediaEnded(object? sender, MediaEndedEventArgs e)
        => OnUiThread(() => HandleMediaEnded(e));

    private void HandleMediaEnded(MediaEndedEventArgs e)
    {
        _ = AdvanceAsync(playNext: AutoPlayEnabled);
    }

    private void OnPlaybackError(object? sender, PlaybackErrorEventArgs e)
        => OnUiThread(() => HandlePlaybackError(e));

    private void HandlePlaybackError(PlaybackErrorEventArgs e)
    {
        var name = e.MediaFile?.DisplayName ?? CurrentSong?.DisplayName ?? "song";
        StatusMessage = $"Playback error: {e.ErrorMessage}";
        _notificationService?.ShowError("Cannot play song", $"{name}: {e.ErrorMessage}. Skipping to the next song.");

        // Skip the broken song
        _ = AdvanceAsync(playNext: true);
    }

    private void OnPlaylistChanged(object? sender, PlaylistChangedEventArgs e)
    {
        // Sync playlist from service when it changes externally
        OnUiThread(SyncPlaylistFromService);
    }

    private void OnFilesAdded(object? sender, MediaFilesChangedEventArgs e)
        => OnUiThread(() => HandleFilesAdded(e));

    private void HandleFilesAdded(MediaFilesChangedEventArgs e)
    {
        var known = new System.Collections.Generic.HashSet<string>(MediaFiles.Select(f => f.Id));
        var added = e.Files.Where(f => known.Add(f.Id)).ToList();
        if (added.Count == 0)
            return;

        SetLibrary(MediaFiles.Concat(added));
        StatusMessage = $"{added.Count} new songs added to library";
    }

    private void OnFilesRemoved(object? sender, MediaFilesChangedEventArgs e)
        => OnUiThread(() => HandleFilesRemoved(e));

    private void HandleFilesRemoved(MediaFilesChangedEventArgs e)
    {
        var removed = new System.Collections.Generic.HashSet<string>(e.Files.Select(f => f.Id));
        SetLibrary(MediaFiles.Where(f => !removed.Contains(f.Id)));
        StatusMessage = $"{e.Files.Count} songs removed from library";
    }

    private void OnFilesModified(object? sender, MediaFilesChangedEventArgs e)
        => OnUiThread(() => HandleFilesModified(e));

    private void HandleFilesModified(MediaFilesChangedEventArgs e)
    {
        var byId = MediaFiles.ToDictionary(f => f.Id);
        foreach (var file in e.Files)
        {
            if (byId.TryGetValue(file.Id, out var existing))
            {
                // The scanner sends entities without metadata: keep what we already have
                // (the extractor is asked to refresh it)
                file.Metadata ??= existing.Metadata;
                byId[file.Id] = file;
            }
        }
        SetLibrary(byId.Values);
        StatusMessage = $"{e.Files.Count} songs updated";
    }

    private void OnMetadataExtracted(object? sender, MetadataExtractedEventArgs e)
        => OnUiThread(() => UpdateAllInstances(e.MediaFile.Id, file =>
        {
            file.Metadata = e.Metadata;
            file.MetadataLoaded = true;
        }));

    private void OnThumbnailGenerated(object? sender, ThumbnailGeneratedEventArgs e)
        => OnUiThread(() => UpdateAllInstances(e.MediaFile.Id, file =>
        {
            file.ThumbnailPath = e.ThumbnailPath;
            file.ThumbnailLoaded = true;
        }));

    /// <summary>
    /// The same file can exist as different MediaFile instances (library list,
    /// search results, playlist): update them all and refresh their bindings.
    /// </summary>
    private void UpdateAllInstances(string mediaFileId, Action<MediaFile> update)
    {
        var instances = new System.Collections.Generic.HashSet<MediaFile>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        foreach (var f in MediaFiles) if (f.Id == mediaFileId) instances.Add(f);
        foreach (var f in FilteredMediaFiles) if (f.Id == mediaFileId) instances.Add(f);
        foreach (var p in CurrentPlaylist) if (p.MediaFile?.Id == mediaFileId) instances.Add(p.MediaFile);
        if (CurrentSong?.Id == mediaFileId) instances.Add(CurrentSong);

        foreach (var file in instances)
        {
            update(file);
            file.NotifyChanged();
        }

        if (CurrentSong?.Id == mediaFileId)
        {
            UpdateCurrentSongInfo();
        }
    }

    private void SyncPlaylistFromService()
    {
        if (_playlistManager == null) return;

        var selected = SelectedPlaylistItem?.PlaylistItem;

        CurrentPlaylist.Clear();
        foreach (var item in _playlistManager.GetCurrentPlaylist())
        {
            if (item.MediaFile != null)
            {
                var vm = new PlaylistItemViewModel(item, item.MediaFile);
                CurrentPlaylist.Add(vm);
                if (ReferenceEquals(item, selected))
                    SelectedPlaylistItem = vm;
            }
        }

        MarkCurrentlyPlaying();
        RefreshCommandStates();
        OnPropertyChanged(nameof(NextSongInfo));
        OnPropertyChanged(nameof(HasNextSong));
    }

    private void UpdateCurrentSongInfo()
    {
        if (CurrentSong != null)
        {
            CurrentSongInfo = CurrentSong.DisplayName;
        }
        else
        {
            CurrentSongInfo = "No song playing";
        }
    }

    [RelayCommand]
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

    [RelayCommand]
    private void ExpandControlHandle()
    {
        if (!IsVideoMode) return;

        IsControlHandleExpanded = true;
        
        // Reset the auto-collapse timer
        _handleCollapseTimer?.Stop();
        _handleCollapseTimer?.Start();
    }

    [RelayCommand]
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

    [RelayCommand]
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

    // ----- Fullscreen / dual screen -------------------------------------------

    /// <summary>
    /// Fullscreen of the window showing the video: the main window (single screen,
    /// together with Video Mode) or the playback window (dual screen).
    /// The App applies it to the actual window.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWindowChrome))]
    private bool _isFullscreen;

    /// <summary>Menu, transport bar and status bar are hidden in single-screen fullscreen.</summary>
    public bool ShowWindowChrome => !(IsFullscreen && !IsDualScreenMode);

    private bool _videoModeBeforeFullscreen;

    [RelayCommand]
    public void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    partial void OnIsFullscreenChanged(bool value)
    {
        if (IsDualScreenMode)
            return;

        if (value)
        {
            _videoModeBeforeFullscreen = IsVideoMode;
            IsVideoMode = true;
        }
        else
        {
            IsVideoMode = _videoModeBeforeFullscreen;
        }
    }

    /// <summary>
    /// Dual screen: video on a separate playback window (second monitor), the main
    /// window keeps catalog and playlist. The App opens/closes the playback window.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoSurfaceAVisible), nameof(IsVideoSurfaceBVisible), nameof(ShowWindowChrome))]
    private bool _isDualScreenMode;

    partial void OnIsDualScreenModeChanged(bool value)
    {
        if (value)
        {
            // Video Mode / fullscreen belong to the playback window now
            if (IsVideoMode) ToggleVideoMode();
            IsFullscreen = false;
        }
        StatusMessage = value ? "Dual screen mode" : "Single screen mode";
    }

    [RelayCommand]
    private void SetSingleScreenMode() => IsDualScreenMode = false;

    [RelayCommand]
    private void SetDualScreenMode() => IsDualScreenMode = true;

    /// <summary>"Artist - Title" of the song that will play next (empty when none).</summary>
    public string NextSongInfo
    {
        get
        {
            var index = IsSongLoaded ? 1 : 0;
            return CurrentPlaylist.Count > index ? CurrentPlaylist[index].MediaFile?.DisplayName ?? string.Empty : string.Empty;
        }
    }

    public bool HasNextSong => !string.IsNullOrEmpty(NextSongInfo);

    public void AddSelectedToPlaylistEnd()
    {
        if (SelectedMediaFile != null)
        {
            _ = AddToPlaylist(SelectedMediaFile, "end");
        }
    }

    public void AddSelectedToPlaylistNext()
    {
        if (SelectedMediaFile != null)
        {
            _ = AddToPlaylist(SelectedMediaFile, "next");
        }
    }

    public void RemoveSelectedFromPlaylist()
    {
        if (SelectedPlaylistItem != null)
        {
            _ = RemoveSong(SelectedPlaylistItem);
        }
    }

    public void FocusSearch()
    {
        // This will be handled by the view
        // Raise an event or use a message bus to notify the view
        StatusMessage = "Search focused";
    }

    [RelayCommand]
    public void OpenPlaylistComposer()
    {
        // This will open the playlist composer window
        StatusMessage = "Opening Playlist Composer...";

        // Use ReactiveUI MessageBus to request window opening
        ReactiveUI.MessageBus.Current.SendMessage(new OpenPlaylistComposerMessage());
    }

    [RelayCommand]
    public void OpenSettings()
    {
        StatusMessage = "Opening Settings...";
        ReactiveUI.MessageBus.Current.SendMessage(new Services.OpenSettingsMessage());
    }

    /// <summary>Re-scans the media folder (changes arrive through the library events).</summary>
    [RelayCommand]
    public async Task RefreshLibrary()
    {
        if (_mediaLibraryManager?.MonitoredDirectory is not { } directory)
        {
            StatusMessage = "No media folder to refresh";
            return;
        }

        StatusMessage = "Refreshing library...";
        try
        {
            await Task.Run(() => _mediaLibraryManager.ScanDirectoryAsync(directory));
            StatusMessage = $"Library refreshed - {MediaLibraryCount} songs";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ToggleDisplayMode() => IsDualScreenMode = !IsDualScreenMode;

    /// <summary>Escape: leave fullscreen first, then Video Mode.</summary>
    public void CloseDialog()
    {
        if (IsFullscreen)
        {
            IsFullscreen = false;
        }
        else if (IsVideoMode)
        {
            ToggleVideoMode();
        }
    }

    [RelayCommand]
    public async Task OpenMediaDirectory()
    {
        // Use ReactiveUI MessageBus to request folder picker
        ReactiveUI.MessageBus.Current.SendMessage(new OpenMediaDirectoryMessage());
    }

    [RelayCommand]
    public void ShowAbout()
    {
        // Use ReactiveUI MessageBus to request about dialog
        ReactiveUI.MessageBus.Current.SendMessage(new ShowAboutMessage());
    }

    [RelayCommand]
    public void Exit()
    {
        // Use ReactiveUI MessageBus to request application exit
        ReactiveUI.MessageBus.Current.SendMessage(new ExitApplicationMessage());
    }

    /// <summary>
    /// Gets the playlist manager for use by child windows
    /// </summary>
    public IPlaylistManager? GetPlaylistManager()
    {
        return _playlistManager;
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _handleCollapseTimer?.Dispose();
    }
}

// Message classes for inter-component communication
public class OpenMediaDirectoryMessage { }
public class ShowAboutMessage { }
public class ExitApplicationMessage { }
