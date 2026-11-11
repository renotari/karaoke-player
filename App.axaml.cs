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
    public IYouTubeService? YouTubeService => _youTubeService;
    private IYouTubeService? _youTubeService;
    private LibVLC? _thumbnailLibVLC;

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
                ShowStartupErrors();

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

    // What failed while starting up: shown to the user once the main window is open
    private readonly System.Collections.Generic.List<string> _startupErrors = new();

    /// <summary>
    /// Creates the services. Each step is independent: without LibVLC the songs do not
    /// play, but the library and the settings must keep working, and the user must be
    /// told what went wrong (a WinExe has no console).
    /// </summary>
    private void InitializeServices()
    {
        void Step(string what, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                _startupErrors.Add($"{what}: {ex.Message}");
                _loggingService?.LogCritical($"Could not initialize {what}", ex);
            }
        }

        Step("logging", () =>
        {
            _loggingService = new LoggingService();
            _loggingService.LogInformation("Application starting");
        });

        Step("notifications", () =>
        {
            _notificationService = new NotificationService();
            _errorHandlingService = new ErrorHandlingService(_notificationService, _loggingService!);

            // Clear all error states on application startup (Requirement 18.13)
            _errorHandlingService.ClearAllErrors();
        });

        Step("settings", () =>
        {
            _settingsManager = new SettingsManager();

            // Settings take effect immediately when they change (Settings dialog, folder picker...)
            _settingsManager.SettingsChanged += (_, _) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplySettings(_settingsManager.GetSettings()));
        });

        Step("keyboard shortcuts", () => _keyboardShortcutManager = new KeyboardShortcutManager());

        Step("YouTube downloads", () => _youTubeService = new YouTubeService(_loggingService));

        Step("media library", () =>
        {
            var userDataPath = AppPaths.Root;

            // Configure SQLite with performance optimizations
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(userDataPath, "karaoke.db"),
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared, // Enable shared cache for connection pooling
                Pooling = true // Enable connection pooling
            }.ToString();

            // Create DbContext factory (handles database initialization and optimizations)
            _dbContextFactory = new DbContextFactory(connectionString);

            _searchEngine = new SearchEngine(_dbContextFactory);
            _playlistManager = new PlaylistManager(_dbContextFactory);
            _mediaLibraryManager = new MediaLibraryManager(_dbContextFactory);
            _metadataExtractor = new MetadataExtractor(_dbContextFactory);

            // Initialize cache manager with cache directory
            _cacheManager = new CacheManager(Path.Combine(userDataPath, "cache"));

            // Files that appear (or change) while the app is running also need
            // metadata and a thumbnail
            _mediaLibraryManager.FilesAdded += OnLibraryFilesChanged;
            _mediaLibraryManager.FilesModified += OnLibraryFilesChanged;
        });

        Step("video player (LibVLC)", () =>
        {
            Core.Initialize();
            _mediaPlayerController = new MediaPlayerController(_loggingService);

            // Create LibVLC instance for thumbnail generator
            if (_dbContextFactory != null)
            {
                // Owned by the App: disposed after the generator on exit
                _thumbnailLibVLC = new LibVLC();
                _thumbnailGenerator = new ThumbnailGenerator(_dbContextFactory, _thumbnailLibVLC);
            }
        });

        if (_startupErrors.Count == 0)
            _loggingService?.LogInformation("All services initialized successfully");
    }

    private void ShowStartupErrors()
    {
        if (_startupErrors.Count == 0)
            return;

        var details = string.Join("\n", _startupErrors);
        if (_mainWindowViewModel != null)
            _mainWindowViewModel.StatusMessage = $"Startup problem - {details}";
        _notificationService?.ShowError("Startup problem", details, 30000);
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
            case nameof(MainWindowViewModel.Volume):
            case nameof(MainWindowViewModel.IsMuted):
                // Keep the settings in memory in step with the slider, so the
                // Settings dialog shows it and applying other settings does not
                // bring back an old volume (saved to disk with the next save / on exit)
                if (_settingsManager != null && _mainWindowViewModel != null)
                    _settingsManager.GetSettings().Volume = _mainWindowViewModel.VolumeToRemember / 100.0;
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
        // (the player may be missing: the library and the settings still work)
        if (_searchEngine != null && _playlistManager != null &&
            _mediaLibraryManager != null && _notificationService != null)
        {
            return new MainWindowViewModel(
                _searchEngine,
                _playlistManager,
                _mediaPlayerController,
                _mediaLibraryManager,
                _notificationService,
                _metadataExtractor,
                _thumbnailGenerator
            )
            {
                YouTubeService = _youTubeService
            };
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
                await LoadSettingsAndScanAsync(); // Creates the defaults and applies them (shortcuts...)
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

    /// <summary>The next time the settings are applied the media folder is scanned again,
    /// even if it is the same one (the user picked it again to reload it).</summary>
    public void ForgetActiveMediaDirectory() => _activeMediaDirectory = null;

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
                _mainWindowViewModel.ApplyVolumeSetting((int)Math.Round(Math.Clamp(settings.Volume, 0, 1) * 100));
                _mainWindowViewModel.AutoPlayEnabled = settings.AutoPlayEnabled;
                _mainWindowViewModel.ShuffleModeEnabled = settings.ShuffleMode;
                _mainWindowViewModel.CrossfadeEnabled = settings.CrossfadeEnabled;
                _mainWindowViewModel.AudioVisualizationEnabled =
                    !string.Equals(settings.VisualizationStyle, "none", StringComparison.OrdinalIgnoreCase);
                _mainWindowViewModel.IsDualScreenMode = settings.DisplayMode == DisplayMode.Dual;
                _mainWindowViewModel.MediaDirectory = settings.MediaDirectory ?? string.Empty;
                _mainWindowViewModel.YouTubeMaxHeight = settings.YouTubeMaxHeight is >= 144 and <= 4320 ? settings.YouTubeMaxHeight : 720;
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

            // Another folder was chosen meanwhile: its own scan takes over
            bool Superseded() => !string.Equals(directory, _activeMediaDirectory, StringComparison.OrdinalIgnoreCase);
            if (Superseded())
                return;

            _mediaLibraryManager.StopMonitoring();
            await _mediaLibraryManager.ScanDirectoryAsync(directory);
            if (Superseded())
                return;
            _mediaLibraryManager.StartMonitoring();

            if (_mediaLibraryManager.SkippedFolders.Count > 0)
            {
                var skipped = _mediaLibraryManager.SkippedFolders;
                _loggingService?.LogWarning($"Folders not readable during the scan: {string.Join("; ", skipped)}");
                _notificationService?.ShowWarning("Some folders could not be read",
                    $"{skipped.Count} folder(s) skipped (songs already in the library were kept), e.g. {skipped[0]}", 10000);
            }

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

            _loggingService?.LogInformation($"Media directory scan complete: {files.Count} files");

            // An unchanged library raises no events: the outcome must be stated anyway
            SetStatus(files.Count > 0 ? $"Library ready - {files.Count} songs" : $"No media files in {directory}");
            if (files.Count == 0)
            {
                _notificationService?.ShowWarning("No media files found",
                    $"No MP4, MKV, WEBM or MP3 files in '{directory}'.");
            }
        }
        catch (Exception ex)
        {
            _activeMediaDirectory = null; // picking the folder again retries
            _loggingService?.LogError("Error scanning media directory", ex);
            SetStatus($"Library scan failed: {ex.Message}");
            _notificationService?.ShowError("Library scan failed", ex.Message);
        }
    }

    private void SetStatus(string message) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        if (_mainWindowViewModel != null)
            _mainWindowViewModel.StatusMessage = message;
    });

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
                settings.Volume = _mainWindowViewModel.VolumeToRemember / 100.0;
                SaveWindowLayout(settings);
                Task.Run(() => _settingsManager.SaveSettingsAsync()).Wait(TimeSpan.FromSeconds(5));
            }
        });

        // The playlist auto-save waits 1 s after a change: write the last edit now
        Step("save playlist", () =>
        {
            if (_playlistManager != null)
                Task.Run(() => _playlistManager.FlushAsync()).Wait(TimeSpan.FromSeconds(5));
        });

        Step("stop background work", () =>
        {
            _mainWindowViewModel?.CancelAllDownloads();
            _metadataExtractor?.StopProcessing();
            _thumbnailGenerator?.StopProcessing();
        });

        Step("dispose player", () => (_mediaPlayerController as IDisposable)?.Dispose());
        Step("dispose library", () => (_mediaLibraryManager as IDisposable)?.Dispose());
        Step("dispose metadata extractor", () => (_metadataExtractor as IDisposable)?.Dispose());
        Step("dispose thumbnail generator", () => (_thumbnailGenerator as IDisposable)?.Dispose());
        Step("dispose thumbnail LibVLC", () => _thumbnailLibVLC?.Dispose());

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
            ("OpenYouTube", vm.OpenYouTube),
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