using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using KaraokePlayer.ViewModels;
using KaraokePlayer.Views;
using LibVLCSharp.Shared;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;

namespace KaraokePlayer;

public partial class App : Application
{
    private IDbContextFactory? _dbContextFactory;
    private ISearchEngine? _searchEngine;
    private IPlaylistManager? _playlistManager;
    private IMediaPlayerController? _mediaPlayerController;
    private IMediaLibraryManager? _mediaLibraryManager;
    private IMetadataExtractor? _metadataExtractor;
    private IThumbnailGenerator? _thumbnailGenerator;
    private ISettingsManager? _settingsManager;
    private IKeyboardShortcutManager? _keyboardShortcutManager;
    private INotificationService? _notificationService;
    private IErrorHandlingService? _errorHandlingService;
    private ILoggingService? _loggingService;
    private ICacheManager? _cacheManager;
    private MainWindowViewModel? _mainWindowViewModel;

    // Public properties for service access
    public static new App? Current => Application.Current as App;
    public ISettingsManager? SettingsManager => _settingsManager;
    public IMediaPlayerController? MediaPlayerController => _mediaPlayerController;
    public ISearchEngine? SearchEngine => _searchEngine;
    public IMediaLibraryManager? MediaLibraryManager => _mediaLibraryManager;
    public ILoggingService? LoggingService => _loggingService;

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

            // Set up global exception handler for ReactiveUI
            // This catches exceptions that occur in reactive code (MessageBus, WhenAnyValue, etc.)
            RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
            {
                Console.WriteLine($"[ReactiveUI] Unhandled exception: {ex.Message}");
                Console.WriteLine($"[ReactiveUI] Exception type: {ex.GetType().Name}");
                Console.WriteLine($"[ReactiveUI] Stack trace: {ex.StackTrace}");

                // Log to file if logging service is available
                _loggingService?.LogError("Unhandled exception in reactive code", ex);

                // Show error to user via notification service if available
                _notificationService?.ShowError(
                    "Application Error",
                    $"An unexpected error occurred: {ex.Message}"
                );
            });

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

            // Window-level effects of the ViewModel state (fullscreen, dual screen)
            _mainWindowViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

            // Cleanup on exit
            desktop.ShutdownRequested += OnShutdownRequested;

            // Check for first run and show welcome dialog after window is ready
            mainWindow.Opened += async (s, e) =>
            {
                var isFirstRun = await CheckFirstRunAsync();
                
                if (isFirstRun)
                {
                    await ShowWelcomeDialogAsync(mainWindow);
                }
                else
                {
                    // Load existing settings and scan media directory
                    await LoadSettingsAndScanAsync();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices()
    {
        try
        {
            // Initialize logging service first so other services can use it
            _loggingService = new LoggingService();
            _loggingService.LogInformation("Application starting");

            // Initialize LibVLC
            Core.Initialize();

            // Set up database with performance optimizations
            var userDataPath = AppPaths.Root;

            var dbPath = Path.Combine(userDataPath, "karaoke.db");
            
            // Configure SQLite with performance optimizations
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared, // Enable shared cache for connection pooling
                Pooling = true // Enable connection pooling
            }.ToString();

            // Create DbContext factory (handles database initialization and optimizations)
            _dbContextFactory = new DbContextFactory(connectionString);

            // Create service instances with logging
            _notificationService = new NotificationService();
            _errorHandlingService = new ErrorHandlingService(_notificationService, _loggingService);
            _settingsManager = new SettingsManager();
            _searchEngine = new SearchEngine(_dbContextFactory);
            _playlistManager = new PlaylistManager(_dbContextFactory);
            _mediaPlayerController = new MediaPlayerController(_loggingService);
            _mediaLibraryManager = new MediaLibraryManager(_dbContextFactory);
            _metadataExtractor = new MetadataExtractor(_dbContextFactory);

            // Create LibVLC instance for thumbnail generator
            var libVLC = new LibVLC();
            _thumbnailGenerator = new ThumbnailGenerator(_dbContextFactory, libVLC);
            _keyboardShortcutManager = new KeyboardShortcutManager();
            
            // Initialize cache manager with cache directory
            var cacheDirectory = Path.Combine(userDataPath, "cache");
            _cacheManager = new CacheManager(cacheDirectory);

            // Settings take effect immediately when they change (Settings dialog, folder picker...)
            _settingsManager.SettingsChanged += (_, _) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplySettings(_settingsManager.GetSettings()));

            // Files that appear (or change) while the app is running also need
            // metadata and a thumbnail
            _mediaLibraryManager.FilesAdded += OnLibraryFilesChanged;
            _mediaLibraryManager.FilesModified += OnLibraryFilesChanged;

            // Clear all error states on application startup (Requirement 18.13)
            _errorHandlingService.ClearAllErrors();
            
            _loggingService.LogInformation("All services initialized successfully");
        }
        catch (Exception ex)
        {
            // Log error - for now just continue with null services
            // The ViewModel will handle null services gracefully
            _loggingService?.LogCritical("Error initializing services", ex);
            Console.WriteLine($"Error initializing services: {ex.Message}");
        }
    }

    // ----- Fullscreen and dual screen -----------------------------------------

    private PlaybackWindow? _playbackWindow;
    private bool _closingPlaybackWindow;
    private Avalonia.Controls.WindowState _mainWindowStateBeforeFullscreen = Avalonia.Controls.WindowState.Normal;

    private MainWindow? MainWindowInstance =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsDualScreenMode):
                ApplyDisplayMode();
                PersistDisplayMode();
                break;
            case nameof(MainWindowViewModel.IsFullscreen):
                ApplyFullscreen();
                break;
        }
    }

    private void ApplyFullscreen()
    {
        var vm = _mainWindowViewModel;
        if (vm == null)
            return;

        if (vm.IsDualScreenMode)
        {
            if (_playbackWindow != null)
            {
                _playbackWindow.WindowState = vm.IsFullscreen
                    ? Avalonia.Controls.WindowState.FullScreen
                    : Avalonia.Controls.WindowState.Normal;
            }
            return;
        }

        if (MainWindowInstance is not { } main)
            return;

        if (vm.IsFullscreen)
        {
            if (main.WindowState != Avalonia.Controls.WindowState.FullScreen)
                _mainWindowStateBeforeFullscreen = main.WindowState;
            main.WindowState = Avalonia.Controls.WindowState.FullScreen;
        }
        else if (main.WindowState == Avalonia.Controls.WindowState.FullScreen)
        {
            main.WindowState = _mainWindowStateBeforeFullscreen;
        }
    }

    /// <summary>Opens or closes the playback window and moves the video accordingly.</summary>
    private void ApplyDisplayMode()
    {
        var vm = _mainWindowViewModel;
        var main = MainWindowInstance;
        if (vm == null || main == null)
            return;

        if (vm.IsDualScreenMode)
        {
            if (_playbackWindow != null)
                return;

            if (main.WindowState == Avalonia.Controls.WindowState.FullScreen)
                main.WindowState = _mainWindowStateBeforeFullscreen;

            var window = new PlaybackWindow { DataContext = vm };
            var onSecondScreen = PlaceOnSecondaryScreen(window, main);
            if (!onSecondScreen && _settingsManager?.GetSettings().PlaybackWindowBounds is { } saved)
                ApplyBounds(window, saved, main);

            window.KeyDown += OnGlobalKeyDown; // shortcuts work there too
            window.Closing += (_, args) =>
            {
                // Closed by the user: go back to single screen first, so the video
                // moves to the main window before this window's surfaces disappear
                if (!_closingPlaybackWindow)
                {
                    args.Cancel = true;
                    vm.IsDualScreenMode = false;
                }
            };
            window.Opened += (_, _) =>
            {
                if (onSecondScreen)
                {
                    window.WindowState = Avalonia.Controls.WindowState.FullScreen;
                    vm.IsFullscreen = true;
                }
            };

            _playbackWindow = window;
            window.Show();
            window.AttachVideoWindows();
        }
        else
        {
            var window = _playbackWindow;
            _playbackWindow = null;
            if (window == null)
                return;

            if (_settingsManager != null && window.WindowState == Avalonia.Controls.WindowState.Normal)
                _settingsManager.GetSettings().PlaybackWindowBounds = BoundsOf(window);

            // Video back to the main window; close the playback window a moment later,
            // once the player has restarted on the new surface
            main.AttachVideoWindows();
            window.Hide();
            Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                _closingPlaybackWindow = true;
                window.Close();
                _closingPlaybackWindow = false;
            }, TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>
    /// Puts the window on a screen other than the main window's one.
    /// Returns false when there is only one screen (normal window next to the main one).
    /// </summary>
    private static bool PlaceOnSecondaryScreen(Window window, Window main)
    {
        var screens = main.Screens;
        var mainScreen = screens.ScreenFromWindow(main) ?? screens.Primary;
        var other = screens.All.FirstOrDefault(s => mainScreen == null || s.Bounds != mainScreen.Bounds);

        if (other != null)
        {
            window.Position = other.WorkingArea.Position;
            return true;
        }

        window.Width = 960;
        window.Height = 540;
        window.Position = new PixelPoint(main.Position.X + 80, main.Position.Y + 80);
        return false;
    }

    // ----- Window layout -----------------------------------------------------

    private static WindowBounds BoundsOf(Window window) => new()
    {
        X = window.Position.X,
        Y = window.Position.Y,
        Width = window.Width,
        Height = window.Height,
        IsMaximized = window.WindowState == Avalonia.Controls.WindowState.Maximized
    };

    /// <summary>True when the saved position is (at least partly) on a connected screen.</summary>
    private static bool IsOnScreen(Window reference, WindowBounds bounds)
    {
        var point = new PixelPoint(bounds.X + 50, bounds.Y + 20);
        return reference.Screens.All.Any(s => s.WorkingArea.Contains(point));
    }

    private static void ApplyBounds(Window window, WindowBounds bounds, Window reference)
    {
        if (bounds.Width >= 400 && bounds.Height >= 300)
        {
            window.Width = bounds.Width;
            window.Height = bounds.Height;
        }
        if (IsOnScreen(reference, bounds))
            window.Position = new PixelPoint(bounds.X, bounds.Y);
        if (bounds.IsMaximized)
            window.WindowState = Avalonia.Controls.WindowState.Maximized;
    }

    private void RestoreWindowLayout(AppSettings settings)
    {
        if (MainWindowInstance is not { } main)
            return;

        try
        {
            if (settings.MainWindowBounds is { } bounds)
                ApplyBounds(main, bounds, main);
            main.SetSideColumnWidths(settings.CatalogColumnWidth, settings.PlaylistColumnWidth);
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning($"Could not restore window layout: {ex.Message}");
        }
    }

    private void SaveWindowLayout(AppSettings settings)
    {
        if (MainWindowInstance is not { } main)
            return;

        if (main.WindowState is Avalonia.Controls.WindowState.Normal or Avalonia.Controls.WindowState.Maximized)
        {
            var bounds = BoundsOf(main);
            if (main.WindowState == Avalonia.Controls.WindowState.Maximized && settings.MainWindowBounds != null)
            {
                // Keep the normal size to come back to when un-maximizing
                bounds.X = settings.MainWindowBounds.X;
                bounds.Y = settings.MainWindowBounds.Y;
                bounds.Width = settings.MainWindowBounds.Width;
                bounds.Height = settings.MainWindowBounds.Height;
            }
            settings.MainWindowBounds = bounds;
        }

        var (catalog, playlist) = main.GetSideColumnWidths();
        settings.CatalogColumnWidth = catalog;
        settings.PlaylistColumnWidth = playlist;

        if (_playbackWindow is { WindowState: Avalonia.Controls.WindowState.Normal } playback)
            settings.PlaybackWindowBounds = BoundsOf(playback);
    }

    private void PersistDisplayMode()
    {
        if (_settingsManager == null || _mainWindowViewModel == null)
            return;

        var mode = _mainWindowViewModel.IsDualScreenMode ? DisplayMode.Dual : DisplayMode.Single;
        if (_settingsManager.GetSettings().DisplayMode == mode)
            return;

        try
        {
            _settingsManager.SetSetting(nameof(AppSettings.DisplayMode), mode);
            _ = _settingsManager.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            _loggingService?.LogError("Could not save display mode", ex);
        }
    }

    private void OnLibraryFilesChanged(object? sender, MediaFilesChangedEventArgs e)
    {
        foreach (var file in e.Files)
        {
            _metadataExtractor?.QueueForExtraction(file);
            _thumbnailGenerator?.QueueForGeneration(file);
        }
    }

    private MainWindowViewModel CreateMainWindowViewModel()
    {
        // If services are available, use the full constructor
        if (_searchEngine != null && _playlistManager != null && 
            _mediaPlayerController != null && _mediaLibraryManager != null &&
            _notificationService != null)
        {
            return new MainWindowViewModel(
                _searchEngine,
                _playlistManager,
                _mediaPlayerController,
                _mediaLibraryManager,
                _notificationService,
                _metadataExtractor,
                _thumbnailGenerator
            );
        }

        // Fallback to design-time constructor
        return new MainWindowViewModel();
    }

    private async Task<bool> CheckFirstRunAsync()
    {
        try
        {
            if (_settingsManager == null)
                return false;

            // Check if settings file exists
            return !File.Exists(AppPaths.SettingsFile);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError("Error checking first run status", ex);
            return false;
        }
    }

    private async Task ShowWelcomeDialogAsync(Window owner)
    {
        try
        {
            _loggingService?.LogInformation("First run detected - showing welcome dialog");

            if (_mediaLibraryManager == null || _metadataExtractor == null || 
                _thumbnailGenerator == null || _settingsManager == null)
            {
                _loggingService?.LogWarning("Services not initialized, skipping welcome dialog");
                return;
            }

            var welcomeDialog = new WelcomeDialog(
                _mediaLibraryManager,
                _metadataExtractor,
                _thumbnailGenerator
            );

            // Show dialog modally with main window as owner
            await welcomeDialog.ShowDialog(owner);

            if (welcomeDialog.ScanCompleted && !string.IsNullOrEmpty(welcomeDialog.SelectedMediaDirectory))
            {
                // Save the selected directory to settings
                _settingsManager.SetSetting(nameof(AppSettings.MediaDirectory), welcomeDialog.SelectedMediaDirectory);
                await _settingsManager.SaveSettingsAsync();
                
                _loggingService?.LogInformation($"First run complete - media directory set to: {welcomeDialog.SelectedMediaDirectory}");
            }
            else
            {
                // User cancelled - create default settings file so app can still run
                // but they can change the directory later in settings
                _loggingService?.LogInformation("Welcome dialog cancelled - creating default settings");
                await _settingsManager.LoadSettingsAsync(); // This will create default settings
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogError("Error showing welcome dialog", ex);
        }
    }

    private async Task LoadSettingsAndScanAsync()
    {
        try
        {
            if (_settingsManager == null)
                return;

            await _settingsManager.LoadSettingsAsync();
            var settings = _settingsManager.GetSettings();
            _loggingService?.LogInformation($"Settings loaded - media directory: {settings.MediaDirectory}");

            RestoreWindowLayout(settings);

            // Applies everything, including the scan of the media directory
            ApplySettings(settings);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError("Error loading settings and scanning", ex);
        }
    }

    // Media directory currently scanned and monitored
    private string? _activeMediaDirectory;

    /// <summary>
    /// Applies the settings to the running application. Called at startup and every
    /// time the settings change. Each step is independent: one failing does not
    /// prevent the others.
    /// </summary>
    private void ApplySettings(AppSettings settings)
    {
        void Try(string what, Action action)
        {
            try { action(); }
            catch (Exception ex) { _loggingService?.LogError($"Could not apply setting: {what}", ex); }
        }

        Try("theme", () =>
            RequestedThemeVariant = string.Equals(settings.Theme, "light", StringComparison.OrdinalIgnoreCase)
                ? Avalonia.Styling.ThemeVariant.Light
                : Avalonia.Styling.ThemeVariant.Dark);

        Try("font size", () =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
                && settings.FontSize is >= 8 and <= 32)
            {
                window.FontSize = settings.FontSize;
            }
        });

        Try("playback", () =>
        {
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.Volume = (int)Math.Round(Math.Clamp(settings.Volume, 0, 1) * 100);
                _mainWindowViewModel.AutoPlayEnabled = settings.AutoPlayEnabled;
                _mainWindowViewModel.ShuffleModeEnabled = settings.ShuffleMode;
                _mainWindowViewModel.CrossfadeEnabled = settings.CrossfadeEnabled;
                _mainWindowViewModel.AudioVisualizationEnabled =
                    !string.Equals(settings.VisualizationStyle, "none", StringComparison.OrdinalIgnoreCase);
                _mainWindowViewModel.IsDualScreenMode = settings.DisplayMode == DisplayMode.Dual;
            }

            if (_mediaPlayerController != null)
            {
                _mediaPlayerController.EnableCrossfade(settings.CrossfadeEnabled, Math.Clamp(settings.CrossfadeDuration, 1, 20));
                _mediaPlayerController.AudioNormalizationEnabled = settings.AudioBoostEnabled;
                _mediaPlayerController.AudioVisualization = settings.VisualizationStyle ?? "bars";
            }
        });

        Try("audio device", () =>
        {
            if (_mediaPlayerController == null)
                return;

            // Settings store the device name shown in the dialog
            var device = _mediaPlayerController.GetAudioDevices()
                .FirstOrDefault(d => d.Name == settings.AudioOutputDevice || d.Id == settings.AudioOutputDevice);
            _mediaPlayerController.SetAudioDevice(device?.Id ?? string.Empty);
        });

        Try("keyboard shortcuts", () =>
        {
            // Defaults first, so actions missing from an older settings file still work
            var shortcuts = AppSettings.GetDefaultKeyboardShortcuts();
            foreach (var (action, key) in settings.KeyboardShortcuts)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    shortcuts[action] = key;
            }
            _keyboardShortcutManager?.LoadShortcuts(shortcuts);
        });

        Try("media directory", () =>
        {
            var directory = settings.MediaDirectory;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                _loggingService?.LogWarning($"Media directory not found or not set: {directory}");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    _notificationService?.ShowWarning("Media folder not found",
                        $"'{directory}' does not exist. Choose another folder in File > Open Media Directory.");
                }
                return;
            }

            if (string.Equals(directory, _activeMediaDirectory, StringComparison.OrdinalIgnoreCase))
                return;

            _activeMediaDirectory = directory;
            _ = Task.Run(() => ScanMediaDirectoryAsync(directory));
        });
    }

    /// <summary>
    /// Scans the media directory (incremental: only new/changed/removed files are
    /// touched), monitors it for changes and queues background metadata/thumbnail work.
    /// Library changes reach the UI through the MediaLibraryManager events.
    /// </summary>
    private async Task ScanMediaDirectoryAsync(string directory)
    {
        if (_mediaLibraryManager == null)
            return;

        try
        {
            _loggingService?.LogInformation($"Scanning media directory {directory}...");

            _mediaLibraryManager.StopMonitoring();
            await _mediaLibraryManager.ScanDirectoryAsync(directory);
            _mediaLibraryManager.StartMonitoring();

            var files = await _mediaLibraryManager.GetMediaFilesAsync();
            foreach (var file in files)
            {
                if (!file.MetadataLoaded)
                    _metadataExtractor?.QueueForExtraction(file);

                if (!file.ThumbnailLoaded)
                    _thumbnailGenerator?.QueueForGeneration(file);
            }

            _metadataExtractor?.StartProcessing();
            _thumbnailGenerator?.StartProcessing();

            _loggingService?.LogInformation("Media directory scan complete");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError("Error scanning media directory", ex);
            _notificationService?.ShowError("Library scan failed", ex.Message);
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _loggingService?.LogInformation("Application shutting down");

        void Step(string what, Action action)
        {
            try { action(); }
            catch (Exception ex) { _loggingService?.LogError($"Shutdown: {what} failed", ex); }
        }

        // Remember the volume set with the slider and the window layout.
        // Saved on a worker thread: waiting here on an async method that resumes
        // on the UI thread would deadlock (the app would never close).
        Step("save settings", () =>
        {
            if (_settingsManager != null && _mainWindowViewModel != null)
            {
                var settings = _settingsManager.GetSettings();
                settings.Volume = _mainWindowViewModel.Volume / 100.0;
                SaveWindowLayout(settings);
                Task.Run(() => _settingsManager.SaveSettingsAsync()).Wait(TimeSpan.FromSeconds(5));
            }
        });

        Step("stop background work", () =>
        {
            _metadataExtractor?.StopProcessing();
            _thumbnailGenerator?.StopProcessing();
        });

        Step("dispose player", () => (_mediaPlayerController as IDisposable)?.Dispose());
        Step("dispose library", () => (_mediaLibraryManager as IDisposable)?.Dispose());
        Step("dispose metadata extractor", () => (_metadataExtractor as IDisposable)?.Dispose());
        Step("dispose thumbnail generator", () => (_thumbnailGenerator as IDisposable)?.Dispose());

        _loggingService?.LogInformation("Application shutdown complete");
    }

    private void SetupKeyboardShortcuts(MainWindow mainWindow)
    {
        if (_keyboardShortcutManager == null || _mainWindowViewModel == null)
            return;

        // Register shortcut handlers (action ids as in AppSettings.KeyboardShortcuts)
        var vm = _mainWindowViewModel;
        var shortcuts = new (string Action, Action Handler)[]
        {
            ("PlayPause", () => vm.PlayPauseCommand.Execute(null)),
            ("Stop", () => vm.StopCommand.Execute(null)),
            ("Next", () => vm.NextCommand.Execute(null)),
            ("Previous", () => vm.PreviousCommand.Execute(null)),
            ("VolumeUp", vm.VolumeUp),
            ("VolumeDown", vm.VolumeDown),
            ("Mute", vm.ToggleMute),
            ("ToggleFullscreen", vm.ToggleFullscreen),
            ("AddToPlaylistEnd", vm.AddSelectedToPlaylistEnd),
            ("AddToPlaylistNext", vm.AddSelectedToPlaylistNext),
            ("RemoveFromPlaylist", vm.RemoveSelectedFromPlaylist),
            ("ClearPlaylist", () => vm.ClearPlaylistCommand.Execute(null)),
            ("ShufflePlaylist", () => vm.ShufflePlaylistCommand.Execute(null)),
            ("FocusSearch", mainWindow.FocusSearchBox),
            ("OpenPlaylistComposer", vm.OpenPlaylistComposer),
            ("OpenSettings", vm.OpenSettings),
            ("RefreshLibrary", () => vm.RefreshLibraryCommand.Execute(null)),
            ("ToggleDisplayMode", vm.ToggleDisplayMode),
            ("CloseDialog", vm.CloseDialog),
        };
        foreach (var (action, handler) in shortcuts)
        {
            _keyboardShortcutManager.RegisterShortcut(action, handler);
        }

        // Attach global key event handler to main window
        mainWindow.KeyDown += OnGlobalKeyDown;
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        // While typing in a text box, plain keys (letters, Space, arrows, Delete...)
        // and the usual editing combinations (Ctrl+A/C/V/X/Z...) belong to the text box
        if (sender is TopLevel topLevel
            && topLevel.FocusManager?.GetFocusedElement() is TextBox
            && IsTextEditingKey(e))
        {
            return;
        }

        _keyboardShortcutManager?.HandleKeyEvent(e);
    }

    private static bool IsTextEditingKey(KeyEventArgs e)
    {
        // Function keys and Escape always work as shortcuts
        if (e.Key is >= Key.F1 and <= Key.F12 || e.Key == Key.Escape)
            return false;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (!ctrl && !alt)
            return true;

        return ctrl && e.Key is Key.A or Key.C or Key.V or Key.X or Key.Z or Key.Y
            or Key.Left or Key.Right or Key.Home or Key.End or Key.Back or Key.Delete;
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