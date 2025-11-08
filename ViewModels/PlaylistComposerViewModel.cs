using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// ViewModel for the Playlist Composer window.
/// Provides functionality for building playlists with catalog browsing, filtering, and drag-and-drop.
/// </summary>
public class PlaylistComposerViewModel : ViewModelBase
{
    private readonly IMediaLibraryManager? _mediaLibraryManager;
    private readonly IPlaylistManager? _playlistManager;
    private Window? _window;
    
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
        // Design-time constructor
        InitializeCollections();
        InitializeCommands();
        SetupPropertyObservers();
    }

    public PlaylistComposerViewModel(IMediaLibraryManager mediaLibraryManager, IPlaylistManager playlistManager)
    {
        _mediaLibraryManager = mediaLibraryManager;
        _playlistManager = playlistManager;
        
        InitializeCollections();
        InitializeCommands();
        SetupPropertyObservers();
        
        // Load catalog files
        LoadCatalogAsync().ConfigureAwait(false);
    }

    private void InitializeCollections()
    {
        CatalogFiles = new ObservableCollection<MediaFile>();
        FilteredCatalogFiles = new ObservableCollection<MediaFile>();
        ComposedPlaylist = new ObservableCollection<MediaFile>();
        SelectedCatalogItems = new ObservableCollection<MediaFile>();
        ArtistFilterOptions = new ObservableCollection<string> { "All Artists" };
    }

    private void InitializeCommands()
    {
        ClearCatalogSearchCommand = ReactiveCommand.Create(ClearCatalogSearch);
        AddSelectedCommand = ReactiveCommand.Create(AddSelected);
        AddSingleSongCommand = ReactiveCommand.Create<MediaFile>(AddSingleSong);
        RemoveCommand = ReactiveCommand.Create<MediaFile>(Remove);
        ClearCommand = ReactiveCommand.CreateFromTask(ClearAsync);
        ShuffleCommand = ReactiveCommand.Create(Shuffle);
        MoveUpCommand = ReactiveCommand.Create(MoveUp);
        MoveDownCommand = ReactiveCommand.Create(MoveDown);
        LoadPlaylistCommand = ReactiveCommand.CreateFromTask(LoadPlaylistAsync);
        SavePlaylistCommand = ReactiveCommand.CreateFromTask(SavePlaylistAsync);
        SaveAndLoadForPlayCommand = ReactiveCommand.CreateFromTask(SaveAndLoadForPlayAsync);
        CloseCommand = ReactiveCommand.Create(Close);
    }

    private void SetupPropertyObservers()
    {
        // Monitor search query changes and apply filtering
        this.WhenAnyValue(x => x.CatalogSearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFiltering());

        // Monitor artist filter changes and apply filtering
        this.WhenAnyValue(x => x.SelectedArtistFilter)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFiltering());

        // Monitor selected catalog items
        SelectedCatalogItems.CollectionChanged += (s, e) =>
        {
            HasSelectedCatalogItems = SelectedCatalogItems.Count > 0;
        };

        // Monitor selected composition item for move buttons
        this.WhenAnyValue(x => x.SelectedCompositionItem)
            .Subscribe(_ => UpdateMoveButtonStates());

        // Monitor composed playlist changes
        ComposedPlaylist.CollectionChanged += (s, e) =>
        {
            UpdateTotalDuration();
            UpdateMoveButtonStates();
        };
    }

    // Properties
    public ObservableCollection<MediaFile> CatalogFiles { get; private set; }
    public ObservableCollection<MediaFile> FilteredCatalogFiles { get; private set; }
    public ObservableCollection<MediaFile> ComposedPlaylist { get; private set; }
    public ObservableCollection<MediaFile> SelectedCatalogItems { get; private set; }
    public ObservableCollection<string> ArtistFilterOptions { get; private set; }

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

    /// <summary>
    /// Sets the window reference for file dialogs
    /// </summary>
    public void SetWindow(Window window)
    {
        _window = window;
    }

    /// <summary>
    /// Loads the media catalog from the library manager
    /// </summary>
    private async Task LoadCatalogAsync()
    {
        if (_mediaLibraryManager == null) return;

        try
        {
            var files = await _mediaLibraryManager.GetMediaFilesAsync();
            
            CatalogFiles.Clear();
            foreach (var file in files)
            {
                CatalogFiles.Add(file);
            }

            // Build artist filter options
            BuildArtistFilterOptions();
            
            // Apply initial filtering
            ApplyFiltering();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading catalog: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the list of unique artists for the filter dropdown
    /// </summary>
    private void BuildArtistFilterOptions()
    {
        ArtistFilterOptions.Clear();
        ArtistFilterOptions.Add("All Artists");

        var artists = CatalogFiles
            .Where(f => f.Metadata != null && !string.IsNullOrWhiteSpace(f.Metadata.Artist))
            .Select(f => f.Metadata!.Artist)
            .Distinct()
            .OrderBy(a => a)
            .ToList();

        foreach (var artist in artists)
        {
            ArtistFilterOptions.Add(artist);
        }
    }

    /// <summary>
    /// Applies search and artist filtering to the catalog
    /// </summary>
    private void ApplyFiltering()
    {
        var filtered = CatalogFiles.AsEnumerable();

        // Apply search query filter
        if (!string.IsNullOrWhiteSpace(CatalogSearchQuery))
        {
            var query = CatalogSearchQuery.ToLowerInvariant();
            filtered = filtered.Where(f =>
                (f.Metadata?.Artist?.ToLowerInvariant().Contains(query) ?? false) ||
                (f.Metadata?.Title?.ToLowerInvariant().Contains(query) ?? false) ||
                f.Filename.ToLowerInvariant().Contains(query)
            );
        }

        // Apply artist filter
        if (!string.IsNullOrWhiteSpace(SelectedArtistFilter) && SelectedArtistFilter != "All Artists")
        {
            filtered = filtered.Where(f => f.Metadata?.Artist == SelectedArtistFilter);
        }

        // Update filtered collection
        FilteredCatalogFiles.Clear();
        foreach (var file in filtered)
        {
            FilteredCatalogFiles.Add(file);
        }
    }

    /// <summary>
    /// Updates the total duration of the composed playlist
    /// </summary>
    private void UpdateTotalDuration()
    {
        var totalSeconds = ComposedPlaylist
            .Where(f => f.Metadata != null)
            .Sum(f => f.Metadata!.Duration);
        
        TotalDuration = TimeSpan.FromSeconds(totalSeconds);
    }

    /// <summary>
    /// Updates the enabled state of move up/down buttons
    /// </summary>
    private void UpdateMoveButtonStates()
    {
        if (SelectedCompositionItem == null || ComposedPlaylist.Count == 0)
        {
            CanMoveUp = false;
            CanMoveDown = false;
            return;
        }

        var index = ComposedPlaylist.IndexOf(SelectedCompositionItem);
        CanMoveUp = index > 0;
        CanMoveDown = index >= 0 && index < ComposedPlaylist.Count - 1;
    }

    // Command implementations

    private void ClearCatalogSearch()
    {
        CatalogSearchQuery = string.Empty;
    }

    private void AddSelected()
    {
        if (SelectedCatalogItems.Count == 0) return;

        foreach (var file in SelectedCatalogItems.ToList())
        {
            ComposedPlaylist.Add(file);
        }
    }

    private void AddSingleSong(MediaFile file)
    {
        if (file == null) return;
        ComposedPlaylist.Add(file);
    }

    private void Remove(MediaFile file)
    {
        if (file == null) return;
        ComposedPlaylist.Remove(file);
    }

    private async Task ClearAsync()
    {
        if (ComposedPlaylist.Count == 0) return;

        // Show confirmation dialog
        if (_window != null)
        {
            var result = await ShowConfirmationDialog(
                "Clear Playlist",
                "Are you sure you want to remove all songs from the composition?"
            );

            if (!result) return;
        }

        ComposedPlaylist.Clear();
    }

    private void Shuffle()
    {
        if (ComposedPlaylist.Count <= 1) return;

        var random = new Random();
        var shuffled = ComposedPlaylist.OrderBy(_ => random.Next()).ToList();
        
        ComposedPlaylist.Clear();
        foreach (var file in shuffled)
        {
            ComposedPlaylist.Add(file);
        }
    }

    private void MoveUp()
    {
        if (SelectedCompositionItem == null) return;

        var index = ComposedPlaylist.IndexOf(SelectedCompositionItem);
        if (index <= 0) return;

        ComposedPlaylist.Move(index, index - 1);
    }

    private void MoveDown()
    {
        if (SelectedCompositionItem == null) return;

        var index = ComposedPlaylist.IndexOf(SelectedCompositionItem);
        if (index < 0 || index >= ComposedPlaylist.Count - 1) return;

        ComposedPlaylist.Move(index, index + 1);
    }

    private async Task LoadPlaylistAsync()
    {
        if (_window == null) return;

        try
        {
            var storageProvider = _window.StorageProvider;
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Playlist",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("M3U Playlist")
                    {
                        Patterns = new[] { "*.m3u", "*.m3u8" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count == 0) return;

            var filePath = files[0].Path.LocalPath;
            await LoadPlaylistFromFileAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading playlist: {ex.Message}");
            await ShowErrorDialog("Load Error", $"Failed to load playlist: {ex.Message}");
        }
    }

    private async Task LoadPlaylistFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var lines = await File.ReadAllLinesAsync(filePath);
        var loadedFiles = new List<MediaFile>();

        foreach (var line in lines)
        {
            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            // Find matching file in catalog
            var matchingFile = CatalogFiles.FirstOrDefault(f => 
                f.FilePath.Equals(line, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(f.FilePath).Equals(Path.GetFileName(line), StringComparison.OrdinalIgnoreCase)
            );

            if (matchingFile != null)
            {
                loadedFiles.Add(matchingFile);
            }
        }

        // Update composed playlist
        ComposedPlaylist.Clear();
        foreach (var file in loadedFiles)
        {
            ComposedPlaylist.Add(file);
        }

        // Set playlist name from filename
        PlaylistName = Path.GetFileNameWithoutExtension(filePath);
    }

    private async Task SavePlaylistAsync()
    {
        if (ComposedPlaylist.Count == 0) return;
        if (_window == null) return;

        try
        {
            var storageProvider = _window.StorageProvider;
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Playlist",
                SuggestedFileName = string.IsNullOrWhiteSpace(PlaylistName) ? "playlist.m3u" : $"{PlaylistName}.m3u",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("M3U Playlist")
                    {
                        Patterns = new[] { "*.m3u" }
                    },
                    new FilePickerFileType("M3U8 Playlist")
                    {
                        Patterns = new[] { "*.m3u8" }
                    }
                }
            });

            if (file == null) return;

            var filePath = file.Path.LocalPath;
            await SavePlaylistToFileAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving playlist: {ex.Message}");
            await ShowErrorDialog("Save Error", $"Failed to save playlist: {ex.Message}");
        }
    }

    private async Task SavePlaylistToFileAsync(string filePath)
    {
        var lines = new List<string>
        {
            "#EXTM3U",
            $"# Playlist: {PlaylistName}",
            $"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            ""
        };

        foreach (var file in ComposedPlaylist)
        {
            // Add extended info
            if (file.Metadata != null)
            {
                var duration = (int)file.Metadata.Duration;
                var artist = file.Metadata.Artist ?? "Unknown";
                var title = file.Metadata.Title ?? file.Filename;
                lines.Add($"#EXTINF:{duration},{artist} - {title}");
            }
            
            // Add file path
            lines.Add(file.FilePath);
        }

        await File.WriteAllLinesAsync(filePath, lines);
    }

    private async Task SaveAndLoadForPlayAsync()
    {
        if (ComposedPlaylist.Count == 0) return;
        if (_playlistManager == null) return;

        try
        {
            // Clear current playlist
            await _playlistManager.ClearPlaylistAsync();

            // Add all songs from composition to the playing playlist
            foreach (var file in ComposedPlaylist)
            {
                await _playlistManager.AddSongAsync(file, "end");
            }

            // Optionally save to file as well
            if (_window != null && !string.IsNullOrWhiteSpace(PlaylistName))
            {
                var userDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "KaraokePlayer",
                    "Playlists"
                );
                Directory.CreateDirectory(userDataPath);

                var filePath = Path.Combine(userDataPath, $"{PlaylistName}.m3u");
                await SavePlaylistToFileAsync(filePath);
            }

            // Close the window
            Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading playlist for play: {ex.Message}");
            await ShowErrorDialog("Load Error", $"Failed to load playlist for playback: {ex.Message}");
        }
    }

    private void Close()
    {
        _window?.Close();
    }

    // Helper methods for dialogs

    private async Task<bool> ShowConfirmationDialog(string title, string message)
    {
        if (_window == null) return false;

        // For now, return true (confirm). In a full implementation, this would show a proper dialog.
        // Avalonia doesn't have built-in MessageBox, so we'd need to create a custom dialog window.
        return true;
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        if (_window == null) return;

        // For now, just log. In a full implementation, this would show a proper error dialog.
        Console.WriteLine($"{title}: {message}");
    }
}
