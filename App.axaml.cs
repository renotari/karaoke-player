using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private IMetadataExtractor? _metadataExtractor;
    private IThumbnailGenerator? _thumbnailGenerator;
    private ISettingsManager? _settingsManager;
    private IKeyboardShortcutManager? _keyboardShortcutManager;
    private INotificationService? _notificationService;
    private IErrorHandlingService? _errorHandlingService;
    private ILoggingService? _loggingService;
    private IWindowManager? _windowManager;
    private IAudioVisualizationEngine? _audioVisualizationEngine;
    private ICacheManager? _cacheManager;
    private MainWindowViewModel? _mainWindowViewModel;

    // Public properties for service access
    public static App? Current => Application.Current as App;
    public ISettingsManager? SettingsManager => _settingsManager;
    public IMediaPlayerController? MediaPlayerController => _mediaPlayerController;
    public ISearchEngine? SearchEngine => _searchEngine;
    public IMediaLibraryManager? MediaLibraryManager => _mediaLibraryManager;

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
            var userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KaraokePlayer"
            );
            Directory.CreateDirectory(userDataPath);

            var dbPath = Path.Combine(userDataPath, "karaoke.db");
            
            // Configure SQLite with performance optimizations
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared, // Enable shared cache for connection pooling
                Pooling = true // Enable connection pooling
            }.ToString();
            
            var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<KaraokeDbContext>();
            optionsBuilder.UseSqlite(connectionString, options =>
            {
                options.CommandTimeout(30);
            });

            _dbContext = new KaraokeDbContext(optionsBuilder.Options);
            _dbContext.Database.EnsureCreated();
            
            // Enable Write-Ahead Logging (WAL) mode for better concurrency
            using (var connection = _dbContext.Database.GetDbConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA journal_mode=WAL;";
                    command.ExecuteNonQuery();
                    
                    // Set other performance pragmas
                    command.CommandText = "PRAGMA synchronous=NORMAL;";
                    command.ExecuteNonQuery();
                    
                    command.CommandText = "PRAGMA cache_size=-64000;"; // 64MB cache
                    command.ExecuteNonQuery();
                    
                    command.CommandText = "PRAGMA temp_store=MEMORY;";
                    command.ExecuteNonQuery();
                }
            }

            // Create service instances with logging
            _notificationService = new NotificationService();
            _errorHandlingService = new ErrorHandlingService(_notificationService, _loggingService);
            _settingsManager = new SettingsManager();
            _searchEngine = new SearchEngine(_dbContext);
            _playlistManager = new PlaylistManager(_dbContext);
            _mediaPlayerController = new MediaPlayerController(_loggingService);
            _mediaLibraryManager = new MediaLibraryManager(_dbContext);
            _metadataExtractor = new MetadataExtractor(_dbContext);
            
            // Create LibVLC instance for thumbnail generator
            var libVLC = new LibVLC();
            _thumbnailGenerator = new ThumbnailGenerator(_dbContext, libVLC);
            _keyboardShortcutManager = new KeyboardShortcutManager();
            
            // Initialize cache manager with cache directory
            var cacheDirectory = Path.Combine(userDataPath, "cache");
            _cacheManager = new CacheManager(cacheDirectory);
            
            // Initialize window manager
            _windowManager = new WindowManager(_settingsManager);
            
            // Initialize audio visualization engine
            _audioVisualizationEngine = new AudioVisualizationEngine();

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
                _notificationService
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
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsPath = Path.Combine(appDataPath, "KaraokePlayer", "settings.json");
            
            return !File.Exists(settingsPath);
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
            if (_settingsManager == null || _mediaLibraryManager == null)
                return;

            // Load settings
            await _settingsManager.LoadSettingsAsync();
            var settings = _settingsManager.GetSettings();

            _loggingService?.LogInformation($"Settings loaded - media directory: {settings.MediaDirectory}");

            // Scan media directory if it exists
            if (!string.IsNullOrEmpty(settings.MediaDirectory) && Directory.Exists(settings.MediaDirectory))
            {
                _loggingService?.LogInformation("Scanning media directory...");
                
                // Perform scan in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _mediaLibraryManager.ScanDirectoryAsync(settings.MediaDirectory);
                        _mediaLibraryManager.StartMonitoring();

                        // Start background processing
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
                    }
                });
            }
            else
            {
                _loggingService?.LogWarning($"Media directory not found or not set: {settings.MediaDirectory}");
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogError("Error loading settings and scanning", ex);
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _loggingService?.LogInformation("Application shutting down");
        
        // Stop background processing
        _metadataExtractor?.StopProcessing();
        _thumbnailGenerator?.StopProcessing();
        
        // Cleanup services
        if (_mediaPlayerController is IDisposable playerDisposable)
        {
            playerDisposable.Dispose();
        }

        if (_mediaLibraryManager is IDisposable libraryDisposable)
        {
            libraryDisposable.Dispose();
        }

        if (_metadataExtractor is IDisposable extractorDisposable)
        {
            extractorDisposable.Dispose();
        }

        if (_thumbnailGenerator is IDisposable generatorDisposable)
        {
            generatorDisposable.Dispose();
        }

        _dbContext?.Dispose();
        
        _loggingService?.LogInformation("Application shutdown complete");
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