using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI; // Keep for throttled search and RxApp.MainThreadScheduler
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// ViewModel for the Playlist Composer window.
/// Provides functionality for building playlists with catalog browsing, filtering, and drag-and-drop.
/// </summary>
public partial class PlaylistComposerViewModel : ViewModelBase, IDisposable
{
    private readonly IMediaLibraryManager? _mediaLibraryManager;
    private readonly IPlaylistManager? _playlistManager;
    private readonly CompositeDisposable _disposables = new();
    private Window? _window;
    
    [ObservableProperty]
    private string _playlistName = string.Empty;
    
    [ObservableProperty]
    private string _catalogSearchQuery = string.Empty;
    
    [ObservableProperty]
    private string? _selectedArtistFilter;
    
    [ObservableProperty]
    private MediaFile? _selectedCompositionItem;
    
    [ObservableProperty]
    private TimeSpan _totalDuration;
    
    [ObservableProperty]
    private bool _hasSelectedCatalogItems;
    
    [ObservableProperty]
    private bool _canMoveUp;
    
    [ObservableProperty]
    private bool _canMoveDown;

    public PlaylistComposerViewModel()
    {
        // Design-time constructor
        InitializeCollections();
        InitializeCommands();
        SetupPropertyObservers();
    }

    // Tells whether a song is playing (it stays on top when the composed playlist is loaded)
    private readonly Func<bool>? _isSongLoaded;

    public PlaylistComposerViewModel(IMediaLibraryManager mediaLibraryManager, IPlaylistManager playlistManager, Func<bool>? isSongLoaded = null)
    {
        _mediaLibraryManager = mediaLibraryManager;
        _playlistManager = playlistManager;
        _isSongLoaded = isSongLoaded;
        
        InitializeCollections();
        InitializeCommands();
        SetupPropertyObservers();
        
        // Load catalog files
        _ = LoadCatalogAsync();
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
        // Commands are auto-generated via RelayCommand attributes
    }

    private void SetupPropertyObservers()
    {
        // Monitor search query changes with throttling (KEEP ReactiveUI for this)
        var throttledSearch = this.WhenAnyValue(x => x.CatalogSearchQuery)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(UiScheduler)
            .Subscribe(_ => ApplyFiltering());
        _disposables.Add(throttledSearch);

        // Artist filter and other property changes now handled via partial OnChanged methods

        // Monitor selected catalog items
        SelectedCatalogItems.CollectionChanged += (s, e) =>
        {
            HasSelectedCatalogItems = SelectedCatalogItems.Count > 0;
        };

        // Selected composition item now handled via partial OnSelectedCompositionItemChanged

        // Monitor composed playlist changes
        ComposedPlaylist.CollectionChanged += (s, e) =>
        {
            UpdateTotalDuration();
            UpdateMoveButtonStates();
        };
    }

    // Properties
    public ObservableCollection<MediaFile> CatalogFiles { get; private set; } = new();
    public ObservableCollection<MediaFile> FilteredCatalogFiles { get; private set; } = new();
    public ObservableCollection<MediaFile> ComposedPlaylist { get; private set; } = new();
    public ObservableCollection<MediaFile> SelectedCatalogItems { get; private set; } = new();
    public ObservableCollection<string> ArtistFilterOptions { get; private set; } = new();

    // Partial methods for property change notifications
    partial void OnCatalogSearchQueryChanged(string value)
    {
        // Throttling handled in SetupPropertyObservers
    }

    partial void OnSelectedArtistFilterChanged(string? value)
    {
        ApplyFiltering();
    }

    partial void OnSelectedCompositionItemChanged(MediaFile? value)
    {
        UpdateMoveButtonStates();
    }

    // Commands are auto-generated via RelayCommand attributes

    /// <summary>
    /// Sets the window reference for file dialogs
    /// </summary>
    public void SetWindow(Window window)
    {
        _window = window;
    }

    /// <summary>Starts from these songs (the current queue) instead of an empty composition.</summary>
    public void SetComposition(IEnumerable<MediaFile> songs)
    {
        ComposedPlaylist.Clear();
        foreach (var song in songs)
            ComposedPlaylist.Add(song);
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

    [RelayCommand]
    private void ClearCatalogSearch()
    {
        CatalogSearchQuery = string.Empty;
    }

    [RelayCommand]
    private void AddSelected()
    {
        if (SelectedCatalogItems.Count == 0) return;

        foreach (var file in SelectedCatalogItems.ToList())
        {
            ComposedPlaylist.Add(file);
        }
    }

    [RelayCommand]
    private void AddSingleSong(MediaFile file)
    {
        if (file == null) return;
        ComposedPlaylist.Add(file);
    }

    [RelayCommand]
    private void Remove(MediaFile file)
    {
        if (file == null) return;
        ComposedPlaylist.Remove(file);
    }

    [RelayCommand]
    private async Task Clear()
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

    [RelayCommand]
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

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedCompositionItem == null) return;

        var index = ComposedPlaylist.IndexOf(SelectedCompositionItem);
        if (index <= 0) return;

        ComposedPlaylist.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedCompositionItem == null) return;

        var index = ComposedPlaylist.IndexOf(SelectedCompositionItem);
        if (index < 0 || index >= ComposedPlaylist.Count - 1) return;

        ComposedPlaylist.Move(index, index + 1);
    }

    [RelayCommand]
    private async Task LoadPlaylist()
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

        var entries = await Task.Run(() => PlaylistFile.ReadEntries(filePath));
        var library = CatalogFiles.ToList();
        var loadedFiles = new List<MediaFile>();
        var missing = new List<string>();

        foreach (var entry in entries)
        {
            var match = PlaylistFile.Resolve(entry, library);
            if (match != null)
                loadedFiles.Add(match);
            else
                missing.Add(entry);
        }

        // Update composed playlist
        ComposedPlaylist.Clear();
        foreach (var file in loadedFiles)
        {
            ComposedPlaylist.Add(file);
        }

        // Set playlist name from filename
        PlaylistName = Path.GetFileNameWithoutExtension(filePath);

        if (missing.Count > 0)
        {
            var list = string.Join("\n", missing.Take(15).Select(Path.GetFileName));
            var more = missing.Count > 15 ? $"\n... and {missing.Count - 15} more" : string.Empty;
            await ShowErrorDialog("Songs not found",
                $"{missing.Count} song(s) of the playlist are not in the library (or more than one file has that name):\n\n{list}{more}");
        }
    }

    [RelayCommand]
    private async Task SavePlaylist()
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

    [RelayCommand]
    private async Task SaveAndLoadForPlay()
    {
        if (ComposedPlaylist.Count == 0) return;
        if (_playlistManager == null) return;

        try
        {
            // Replace the queue (the song playing, if any, keeps playing on top)
            await _playlistManager.ClearPlaylistAsync(keepFirst: _isSongLoaded?.Invoke() == true);

            // Add all songs from composition to the playing playlist
            foreach (var file in ComposedPlaylist)
            {
                await _playlistManager.AddSongAsync(file, "end");
            }

            // Also saved in the Playlists folder (the name cannot point elsewhere)
            var name = PlaylistFile.SafeFileName(PlaylistName ?? string.Empty);
            if (_window != null && name.Length > 0)
            {
                try
                {
                    await SavePlaylistToFileAsync(Path.Combine(AppPaths.Playlists, $"{name}.m3u"));
                }
                catch (Exception ex)
                {
                    // The queue is already loaded: say only that the copy was not saved
                    await ShowErrorDialog("Playlist not saved",
                        $"The songs are in the queue, but the playlist could not be saved: {ex.Message}");
                }
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

    [RelayCommand]
    private void Close()
    {
        _window?.Close();
    }

    // Helper methods for dialogs

    private async Task<bool> ShowConfirmationDialog(string title, string message)
    {
        if (_window == null) return false;
        return await Views.MessageDialog.ConfirmAsync(_window, title, message, "Clear", "Cancel");
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        App.Current?.LoggingService?.LogWarning($"{title}: {message}");
        if (_window == null) return;
        await Views.MessageDialog.ShowAsync(_window, title, message);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
