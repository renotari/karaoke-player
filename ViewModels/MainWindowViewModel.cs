using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using KaraokePlayer.Models;

namespace KaraokePlayer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private string _searchQuery = string.Empty;
    private MediaFile? _selectedMediaFile;
    private PlaylistItemViewModel? _selectedPlaylistItem;
    private bool _isPlaying;
    private double _currentTime;
    private double _duration;
    private int _volume = 75;
    private string _currentSongInfo = "No song playing";
    private string _statusMessage = "Ready";
    private bool _shuffleEnabled;
    private bool _crossfadeEnabled;
    private int _mediaLibraryCount;

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
            .Subscribe(FilterMediaFiles);

        // Initialize commands
        ClearSearchCommand = ReactiveCommand.Create(ClearSearch);
        AddToPlaylistNextCommand = ReactiveCommand.Create<MediaFile>(AddToPlaylistNext);
        AddToPlaylistEndCommand = ReactiveCommand.Create<MediaFile>(AddToPlaylistEnd);
        RemoveSongCommand = ReactiveCommand.Create<PlaylistItemViewModel>(RemoveSong);
        
        // Commands with observable conditions
        var canClearPlaylist = this.WhenAnyValue(
            x => x.CurrentPlaylist.Count,
            count => count > 0);
        ClearPlaylistCommand = ReactiveCommand.Create(ClearPlaylist, canClearPlaylist);
        
        var canShufflePlaylist = this.WhenAnyValue(
            x => x.CurrentPlaylist.Count,
            count => count > 1);
        ShufflePlaylistCommand = ReactiveCommand.Create(ShufflePlaylist, canShufflePlaylist);
        PlayPauseCommand = ReactiveCommand.Create(PlayPause);
        StopCommand = ReactiveCommand.Create(Stop);
        NextCommand = ReactiveCommand.Create(Next);
        PreviousCommand = ReactiveCommand.Create(Previous);

        // Load sample data for design-time preview
        LoadSampleData();
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
        set => this.RaiseAndSetIfChanged(ref _volume, value);
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

    // Collections
    public ObservableCollection<MediaFile> MediaFiles { get; }
    public ObservableCollection<MediaFile> FilteredMediaFiles { get; }
    public ObservableCollection<PlaylistItemViewModel> CurrentPlaylist { get; }

    // Commands
    public ReactiveCommand<Unit, Unit> ClearSearchCommand { get; }
    public ReactiveCommand<MediaFile, Unit> AddToPlaylistNextCommand { get; }
    public ReactiveCommand<MediaFile, Unit> AddToPlaylistEndCommand { get; }
    public ReactiveCommand<PlaylistItemViewModel, Unit> RemoveSongCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearPlaylistCommand { get; }
    public ReactiveCommand<Unit, Unit> ShufflePlaylistCommand { get; }
    public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }

    // Command implementations
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    private void FilterMediaFiles(string? query)
    {
        FilteredMediaFiles.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var file in MediaFiles)
            {
                FilteredMediaFiles.Add(file);
            }
            return;
        }

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

    private void AddToPlaylistNext(MediaFile mediaFile)
    {
        var playlistItem = new PlaylistItem
        {
            MediaFileId = mediaFile.Id,
            MediaFile = mediaFile,
            Position = CurrentPlaylist.Count > 0 ? 1 : 0,
            IsDuplicate = CurrentPlaylist.Any(p => p.PlaylistItem.MediaFileId == mediaFile.Id)
        };

        var viewModel = new PlaylistItemViewModel(playlistItem);

        if (CurrentPlaylist.Count > 0)
        {
            CurrentPlaylist.Insert(1, viewModel);
            // Update positions
            for (int i = 0; i < CurrentPlaylist.Count; i++)
            {
                CurrentPlaylist[i].PlaylistItem.Position = i;
            }
        }
        else
        {
            CurrentPlaylist.Add(viewModel);
        }

        StatusMessage = $"Added '{mediaFile.Metadata?.Title ?? mediaFile.Filename}' to playlist (next)";
    }

    private void AddToPlaylistEnd(MediaFile mediaFile)
    {
        var playlistItem = new PlaylistItem
        {
            MediaFileId = mediaFile.Id,
            MediaFile = mediaFile,
            Position = CurrentPlaylist.Count,
            IsDuplicate = CurrentPlaylist.Any(p => p.PlaylistItem.MediaFileId == mediaFile.Id)
        };

        var viewModel = new PlaylistItemViewModel(playlistItem);
        CurrentPlaylist.Add(viewModel);
        StatusMessage = $"Added '{mediaFile.Metadata?.Title ?? mediaFile.Filename}' to playlist (end)";
    }

    private void RemoveSong(PlaylistItemViewModel item)
    {
        CurrentPlaylist.Remove(item);
        // Update positions
        for (int i = 0; i < CurrentPlaylist.Count; i++)
        {
            CurrentPlaylist[i].PlaylistItem.Position = i;
        }
        StatusMessage = "Song removed from playlist";
    }

    private void ClearPlaylist()
    {
        CurrentPlaylist.Clear();
        StatusMessage = "Playlist cleared";
    }

    private void ShufflePlaylist()
    {
        var random = new Random();
        var shuffled = CurrentPlaylist.OrderBy(x => random.Next()).ToList();
        CurrentPlaylist.Clear();
        for (int i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].PlaylistItem.Position = i;
            CurrentPlaylist.Add(shuffled[i]);
        }
        StatusMessage = "Playlist shuffled";
    }

    private void PlayPause()
    {
        IsPlaying = !IsPlaying;
        StatusMessage = IsPlaying ? "Playing" : "Paused";
    }

    private void Stop()
    {
        IsPlaying = false;
        CurrentTime = 0;
        StatusMessage = "Stopped";
    }

    private void Next()
    {
        StatusMessage = "Next song";
        // TODO: Implement with MediaPlayerController
    }

    private void Previous()
    {
        StatusMessage = "Previous song";
        // TODO: Implement with MediaPlayerController
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
}
