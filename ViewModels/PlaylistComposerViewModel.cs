using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using KaraokePlayer.Models;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// ViewModel for the Playlist Composer window.
/// This is a stub implementation - full implementation is in task 21.
/// </summary>
public class PlaylistComposerViewModel : ViewModelBase
{
    private string _playlistName = string.Empty;
    private string _catalogSearchQuery = string.Empty;
    private string? _selectedArtistFilter;
    private MediaFile? _selectedCompositionItem;
    private TimeSpan _totalDuration;
    private bool _hasSelectedCatalogItems;
    private bool _canMoveUp;
    private bool _canMoveDown;

    public PlaylistComposerViewModel()
    {
        // Initialize collections
        CatalogFiles = new ObservableCollection<MediaFile>();
        FilteredCatalogFiles = new ObservableCollection<MediaFile>();
        ComposedPlaylist = new ObservableCollection<MediaFile>();
        SelectedCatalogItems = new ObservableCollection<MediaFile>();
        ArtistFilterOptions = new ObservableCollection<string>();

        // Initialize commands
        InitializeCommands();
    }

    private void InitializeCommands()
    {
        ClearCatalogSearchCommand = ReactiveCommand.Create(ClearCatalogSearch);
        AddSelectedCommand = ReactiveCommand.Create(AddSelected);
        AddSingleSongCommand = ReactiveCommand.Create<MediaFile>(AddSingleSong);
        RemoveCommand = ReactiveCommand.Create<MediaFile>(Remove);
        ClearCommand = ReactiveCommand.Create(Clear);
        ShuffleCommand = ReactiveCommand.Create(Shuffle);
        MoveUpCommand = ReactiveCommand.Create(MoveUp);
        MoveDownCommand = ReactiveCommand.Create(MoveDown);
        LoadPlaylistCommand = ReactiveCommand.Create(LoadPlaylist);
        SavePlaylistCommand = ReactiveCommand.Create(SavePlaylist);
        SaveAndLoadForPlayCommand = ReactiveCommand.Create(SaveAndLoadForPlay);
        CloseCommand = ReactiveCommand.Create(Close);
    }

    // Properties
    public ObservableCollection<MediaFile> CatalogFiles { get; }
    public ObservableCollection<MediaFile> FilteredCatalogFiles { get; }
    public ObservableCollection<MediaFile> ComposedPlaylist { get; }
    public ObservableCollection<MediaFile> SelectedCatalogItems { get; }
    public ObservableCollection<string> ArtistFilterOptions { get; }

    public string PlaylistName
    {
        get => _playlistName;
        set => this.RaiseAndSetIfChanged(ref _playlistName, value);
    }

    public string CatalogSearchQuery
    {
        get => _catalogSearchQuery;
        set => this.RaiseAndSetIfChanged(ref _catalogSearchQuery, value);
    }

    public string? SelectedArtistFilter
    {
        get => _selectedArtistFilter;
        set => this.RaiseAndSetIfChanged(ref _selectedArtistFilter, value);
    }

    public MediaFile? SelectedCompositionItem
    {
        get => _selectedCompositionItem;
        set => this.RaiseAndSetIfChanged(ref _selectedCompositionItem, value);
    }

    public TimeSpan TotalDuration
    {
        get => _totalDuration;
        set => this.RaiseAndSetIfChanged(ref _totalDuration, value);
    }

    public bool HasSelectedCatalogItems
    {
        get => _hasSelectedCatalogItems;
        set => this.RaiseAndSetIfChanged(ref _hasSelectedCatalogItems, value);
    }

    public bool CanMoveUp
    {
        get => _canMoveUp;
        set => this.RaiseAndSetIfChanged(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        set => this.RaiseAndSetIfChanged(ref _canMoveDown, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> ClearCatalogSearchCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> AddSelectedCommand { get; private set; }
    public ReactiveCommand<MediaFile, Unit> AddSingleSongCommand { get; private set; }
    public ReactiveCommand<MediaFile, Unit> RemoveCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ShuffleCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> MoveUpCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> MoveDownCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> LoadPlaylistCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> SavePlaylistCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> SaveAndLoadForPlayCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; private set; }

    // Command implementations (stubs)
    private void ClearCatalogSearch()
    {
        CatalogSearchQuery = string.Empty;
    }

    private void AddSelected()
    {
        // Stub - will be implemented in task 21
    }

    private void AddSingleSong(MediaFile file)
    {
        // Stub - will be implemented in task 21
    }

    private void Remove(MediaFile file)
    {
        // Stub - will be implemented in task 21
    }

    private void Clear()
    {
        // Stub - will be implemented in task 21
    }

    private void Shuffle()
    {
        // Stub - will be implemented in task 21
    }

    private void MoveUp()
    {
        // Stub - will be implemented in task 21
    }

    private void MoveDown()
    {
        // Stub - will be implemented in task 21
    }

    private void LoadPlaylist()
    {
        // Stub - will be implemented in task 21
    }

    private void SavePlaylist()
    {
        // Stub - will be implemented in task 21
    }

    private void SaveAndLoadForPlay()
    {
        // Stub - will be implemented in task 21
    }

    private void Close()
    {
        // Stub - will be implemented in task 21
    }
}
