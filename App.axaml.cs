using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using KaraokePlayer.ViewModels;
using KaraokePlayer.Views;
using LibVLCSharp.Shared;
using Microsoft.EntityFrameworkCore;

namespace KaraokePlayer;

public partial class App : Application
{
    private KaraokeDbContext? _dbContext;
    private ISearchEngine? _searchEngine;
    private IPlaylistManager? _playlistManager;
    private IMediaPlayerController? _mediaPlayerController;
    private IMediaLibraryManager? _mediaLibraryManager;
    private IKeyboardShortcutManager? _keyboardShortcutManager;
    private MainWindowViewModel? _mainWindowViewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Initialize services
            InitializeServices();

            // Create main window with services injected
            _mainWindowViewModel = CreateMainWindowViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = _mainWindowViewModel,
            };
            desktop.MainWindow = mainWindow;

            // Set up global keyboard shortcuts
            SetupKeyboardShortcuts(mainWindow);

            // Cleanup on exit
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices()
    {
        try
        {
            // Initialize LibVLC
            Core.Initialize();

            // Set up database
            var userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KaraokePlayer"
            );
            Directory.CreateDirectory(userDataPath);

            var dbPath = Path.Combine(userDataPath, "karaoke.db");
            var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<KaraokeDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            _dbContext = new KaraokeDbContext(optionsBuilder.Options);
            _dbContext.Database.EnsureCreated();

            // Create service instances
            _searchEngine = new SearchEngine(_dbContext);
            _playlistManager = new PlaylistManager(_dbContext);
            _mediaPlayerController = new MediaPlayerController();
            _mediaLibraryManager = new MediaLibraryManager(_dbContext);
            _keyboardShortcutManager = new KeyboardShortcutManager();
        }
        catch (Exception ex)
        {
            // Log error - for now just continue with null services
            // The ViewModel will handle null services gracefully
            Console.WriteLine($"Error initializing services: {ex.Message}");
        }
    }

    private MainWindowViewModel CreateMainWindowViewModel()
    {
        // If services are available, use the full constructor
        if (_searchEngine != null && _playlistManager != null && 
            _mediaPlayerController != null && _mediaLibraryManager != null)
        {
            return new MainWindowViewModel(
                _searchEngine,
                _playlistManager,
                _mediaPlayerController,
                _mediaLibraryManager
            );
        }

        // Fallback to design-time constructor
        return new MainWindowViewModel();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Cleanup services
        if (_mediaPlayerController is IDisposable playerDisposable)
        {
            playerDisposable.Dispose();
        }

        if (_mediaLibraryManager is IDisposable libraryDisposable)
        {
            libraryDisposable.Dispose();
        }

        _dbContext?.Dispose();
    }

    private void SetupKeyboardShortcuts(MainWindow mainWindow)
    {
        if (_keyboardShortcutManager == null || _mainWindowViewModel == null)
            return;

        // Register shortcut handlers
        _keyboardShortcutManager.RegisterShortcut("PlayPause", () => _mainWindowViewModel.PlayPauseCommand?.Execute(System.Reactive.Unit.Default));
        _keyboardShortcutManager.RegisterShortcut("Stop", () => _mainWindowViewModel.StopCommand?.Execute(System.Reactive.Unit.Default));
        _keyboardShortcutManager.RegisterShortcut("Next", () => _mainWindowViewModel.NextCommand?.Execute(System.Reactive.Unit.Default));
        _keyboardShortcutManager.RegisterShortcut("Previous", () => _mainWindowViewModel.PreviousCommand?.Execute(System.Reactive.Unit.Default));
        _keyboardShortcutManager.RegisterShortcut("VolumeUp", () => _mainWindowViewModel.VolumeUp());
        _keyboardShortcutManager.RegisterShortcut("VolumeDown", () => _mainWindowViewModel.VolumeDown());
        _keyboardShortcutManager.RegisterShortcut("Mute", () => _mainWindowViewModel.ToggleMute());
        _keyboardShortcutManager.RegisterShortcut("ToggleFullscreen", () => _mainWindowViewModel.ToggleFullscreen());
        _keyboardShortcutManager.RegisterShortcut("AddToPlaylistEnd", () => _mainWindowViewModel.AddSelectedToPlaylistEnd());
        _keyboardShortcutManager.RegisterShortcut("AddToPlaylistNext", () => _mainWindowViewModel.AddSelectedToPlaylistNext());
        _keyboardShortcutManager.RegisterShortcut("RemoveFromPlaylist", () => _mainWindowViewModel.RemoveSelectedFromPlaylist());
        _keyboardShortcutManager.RegisterShortcut("ClearPlaylist", () => _mainWindowViewModel.ClearPlaylistCommand?.Execute(System.Reactive.Unit.Default));
        _keyboardShortcutManager.RegisterShortcut("ShufflePlaylist", () => _mainWindowViewModel.ShufflePlaylistCommand?.Execute(System.Reactive.Unit.Default));
        _keyboardShortcutManager.RegisterShortcut("FocusSearch", () => mainWindow.FocusSearchBox());
        _keyboardShortcutManager.RegisterShortcut("OpenPlaylistComposer", () => _mainWindowViewModel.OpenPlaylistComposer());
        _keyboardShortcutManager.RegisterShortcut("OpenSettings", () => _mainWindowViewModel.OpenSettings());
        _keyboardShortcutManager.RegisterShortcut("RefreshLibrary", () => _mainWindowViewModel.RefreshLibrary());
        _keyboardShortcutManager.RegisterShortcut("ToggleDisplayMode", () => _mainWindowViewModel.ToggleDisplayMode());
        _keyboardShortcutManager.RegisterShortcut("CloseDialog", () => _mainWindowViewModel.CloseDialog());

        // Attach global key event handler to main window
        mainWindow.KeyDown += OnGlobalKeyDown;
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        // Let the keyboard shortcut manager handle the event
        _keyboardShortcutManager?.HandleKeyEvent(e);
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}