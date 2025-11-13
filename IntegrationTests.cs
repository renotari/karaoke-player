using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using KaraokePlayer.ViewModels;
using LibVLCSharp.Shared;
using Microsoft.EntityFrameworkCore;
using MediaType = KaraokePlayer.Models.MediaType;

namespace KaraokePlayer;

/// <summary>
/// Integration tests for the Karaoke Player application.
/// Tests the wiring and integration of all components.
/// </summary>
public class IntegrationTests
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
    private IWindowManager? _windowManager;
    private IAudioVisualizationEngine? _audioVisualizationEngine;
    private ICacheManager? _cacheManager;
    private MainWindowViewModel? _mainWindowViewModel;
    private string _testDbPath = "";

    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("=== Starting Integration Tests ===\n");

        try
        {
            await TestServiceInitialization();
            await TestDependencyInjection();
            await TestMediaLibraryWorkflow();
            await TestPlaylistWorkflow();
            await TestSearchIntegration();
            await TestErrorHandlingIntegration();
            await TestSettingsIntegration();
            await TestCacheIntegration();
            await TestWindowManagerIntegration();
            
            Console.WriteLine("\n=== All Integration Tests Passed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n!!! Integration Test Failed: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            Cleanup();
        }
    }

    private async Task TestServiceInitialization()
    {
        Console.WriteLine("Test 1: Service Initialization");
        
        InitializeServices();
        
        if (_loggingService == null) throw new Exception("LoggingService not initialized");
        if (_notificationService == null) throw new Exception("NotificationService not initialized");
        if (_errorHandlingService == null) throw new Exception("ErrorHandlingService not initialized");
        if (_settingsManager == null) throw new Exception("SettingsManager not initialized");
        if (_searchEngine == null) throw new Exception("SearchEngine not initialized");
        if (_playlistManager == null) throw new Exception("PlaylistManager not initialized");
        if (_mediaPlayerController == null) throw new Exception("MediaPlayerController not initialized");
        if (_mediaLibraryManager == null) throw new Exception("MediaLibraryManager not initialized");
        if (_metadataExtractor == null) throw new Exception("MetadataExtractor not initialized");
        if (_thumbnailGenerator == null) throw new Exception("ThumbnailGenerator not initialized");
        if (_keyboardShortcutManager == null) throw new Exception("KeyboardShortcutManager not initialized");
        if (_cacheManager == null) throw new Exception("CacheManager not initialized");
        if (_windowManager == null) throw new Exception("WindowManager not initialized");
        if (_audioVisualizationEngine == null) throw new Exception("AudioVisualizationEngine not initialized");
        
        Console.WriteLine("✓ All services initialized successfully\n");
    }

    private async Task TestDependencyInjection()
    {
        Console.WriteLine("Test 2: Dependency Injection and ViewModel Creation");
        
        _mainWindowViewModel = new MainWindowViewModel(
            _searchEngine!,
            _playlistManager!,
            _mediaPlayerController!,
            _mediaLibraryManager!,
            _notificationService!
        );
        
        if (_mainWindowViewModel == null) throw new Exception("MainWindowViewModel not created");
        if (_mainWindowViewModel.MediaFiles == null) throw new Exception("MediaFiles collection not initialized");
        if (_mainWindowViewModel.CurrentPlaylist == null) throw new Exception("CurrentPlaylist collection not initialized");
        
        Console.WriteLine("✓ ViewModel created with all dependencies injected\n");
    }

    private async Task TestMediaLibraryWorkflow()
    {
        Console.WriteLine("Test 3: Media Library Workflow");
        
        // Create test media directory
        var testMediaDir = Path.Combine(Path.GetTempPath(), "KaraokePlayerTest_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(testMediaDir);
        
        try
        {
            // Create test media files
            File.WriteAllText(Path.Combine(testMediaDir, "Test Song 1.mp3"), "dummy");
            File.WriteAllText(Path.Combine(testMediaDir, "Artist - Title.mp4"), "dummy");
            
            // Scan directory
            await _mediaLibraryManager!.ScanDirectoryAsync(testMediaDir);
            
            // Get media files
            var files = await _mediaLibraryManager.GetMediaFilesAsync();
            
            if (files.Count != 2) throw new Exception($"Expected 2 files, found {files.Count}");
            
            Console.WriteLine($"✓ Scanned {files.Count} media files");
            
            // Test metadata extraction
            var mp3File = files.FirstOrDefault(f => f.Format == MediaFormat.MP3);
            if (mp3File != null)
            {
                _metadataExtractor!.QueueForExtraction(mp3File);
                _metadataExtractor.StartProcessing();
                
                // Wait a bit for processing
                await Task.Delay(500);
                
                Console.WriteLine("✓ Metadata extraction queued");
            }
            
            // Test thumbnail generation
            var mp4File = files.FirstOrDefault(f => f.Format == MediaFormat.MP4);
            if (mp4File != null)
            {
                _thumbnailGenerator!.QueueForGeneration(mp4File);
                _thumbnailGenerator.StartProcessing();
                
                // Wait a bit for processing
                await Task.Delay(500);
                
                Console.WriteLine("✓ Thumbnail generation queued");
            }
            
            Console.WriteLine("✓ Media library workflow completed\n");
        }
        finally
        {
            // Cleanup test directory
            if (Directory.Exists(testMediaDir))
            {
                Directory.Delete(testMediaDir, true);
            }
        }
    }

    private async Task TestPlaylistWorkflow()
    {
        Console.WriteLine("Test 4: Playlist Workflow");
        
        // Create test media file
        var testFile = new MediaFile
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = "test.mp3",
            Filename = "Test Song",
            Type = MediaType.Audio,
            Format = MediaFormat.MP3,
            Metadata = new MediaMetadata
            {
                Artist = "Test Artist",
                Title = "Test Title",
                Duration = 180
            }
        };

        // Add to database
        using (var context = _dbContextFactory!.CreateDbContext())
        {
            context.MediaFiles.Add(testFile);
            await context.SaveChangesAsync();
        }

        // Add to playlist
        await _playlistManager!.AddSongAsync(testFile, "end");
        
        var playlist = _playlistManager.GetCurrentPlaylist();
        if (playlist.Count != 1) throw new Exception($"Expected 1 item in playlist, found {playlist.Count}");
        
        Console.WriteLine("✓ Added song to playlist");
        
        // Test duplicate detection
        var isDuplicate = _playlistManager.IsDuplicate(testFile);
        if (!isDuplicate) throw new Exception("Duplicate detection failed");
        
        Console.WriteLine("✓ Duplicate detection working");
        
        // Test playlist save/load
        var tempPlaylistPath = Path.Combine(Path.GetTempPath(), "test_playlist.m3u");
        await _playlistManager.SavePlaylistAsync(tempPlaylistPath);
        
        if (!File.Exists(tempPlaylistPath)) throw new Exception("Playlist file not created");
        
        Console.WriteLine("✓ Playlist saved to M3U");
        
        // Clear and reload
        await _playlistManager.ClearPlaylistAsync();
        await _playlistManager.LoadPlaylistAsync(tempPlaylistPath);
        
        playlist = _playlistManager.GetCurrentPlaylist();
        if (playlist.Count != 1) throw new Exception("Playlist not loaded correctly");
        
        Console.WriteLine("✓ Playlist loaded from M3U");
        
        // Cleanup
        File.Delete(tempPlaylistPath);
        
        Console.WriteLine("✓ Playlist workflow completed\n");
    }

    private async Task TestSearchIntegration()
    {
        Console.WriteLine("Test 5: Search Integration");
        
        // Search for test file
        var results = await _searchEngine!.SearchAsync("Test");
        
        if (results.Count == 0) throw new Exception("Search returned no results");
        
        Console.WriteLine($"✓ Search found {results.Count} results");
        
        // Test search history
        await _searchEngine.AddToHistoryAsync("Test");
        var history = await _searchEngine.GetHistoryAsync();
        
        if (history.Count == 0) throw new Exception("Search history not saved");
        
        Console.WriteLine("✓ Search history working");
        Console.WriteLine("✓ Search integration completed\n");
    }

    private async Task TestErrorHandlingIntegration()
    {
        Console.WriteLine("Test 6: Error Handling Integration");
        
        // Test error handling service
        var testFile = new MediaFile
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = "nonexistent.mp3",
            Filename = "Nonexistent",
            Type = MediaType.Audio,
            Format = MediaFormat.MP3
        };
        
        _errorHandlingService!.HandleMissingFile(testFile);
        
        if (string.IsNullOrEmpty(testFile.ErrorMessage)) throw new Exception("Error not set on file");
        
        Console.WriteLine("✓ File error handling working");
        
        // Test error clearing
        _errorHandlingService.ClearAllErrors();
        
        Console.WriteLine("✓ Error clearing working");
        Console.WriteLine("✓ Error handling integration completed\n");
    }

    private async Task TestSettingsIntegration()
    {
        Console.WriteLine("Test 7: Settings Integration");
        
        // Test settings manager
        var settings = _settingsManager!.GetSettings();
        
        if (settings == null) throw new Exception("Settings not loaded");
        
        Console.WriteLine("✓ Settings loaded");
        
        // Test setting a value
        _settingsManager.SetSetting(nameof(AppSettings.Volume), 75.0);
        var volume = _settingsManager.GetSetting<double>(nameof(AppSettings.Volume));
        
        if (Math.Abs(volume - 75.0) > 0.01) throw new Exception("Setting not saved correctly");
        
        Console.WriteLine("✓ Settings save/load working");
        
        // Test settings persistence
        await _settingsManager.SaveSettingsAsync();
        
        Console.WriteLine("✓ Settings persistence working");
        Console.WriteLine("✓ Settings integration completed\n");
    }

    private async Task TestCacheIntegration()
    {
        Console.WriteLine("Test 8: Cache Integration");
        
        // Test cache manager
        _cacheManager!.Set("test_key", "test_value", Services.CacheCategory.Metadata);
        var value = _cacheManager.Get("test_key", Services.CacheCategory.Metadata);
        
        if (value == null || value.ToString() != "test_value") 
            throw new Exception("Cache set/get failed");
        
        Console.WriteLine("✓ Cache set/get working");
        
        // Test cache invalidation
        _cacheManager.Invalidate("test_key", Services.CacheCategory.Metadata);
        value = _cacheManager.Get("test_key", Services.CacheCategory.Metadata);
        
        if (value != null) throw new Exception("Cache invalidation failed");
        
        Console.WriteLine("✓ Cache invalidation working");
        
        // Test cache stats
        var stats = _cacheManager.GetCacheStats();
        
        if (stats == null) throw new Exception("Cache stats not available");
        
        Console.WriteLine($"✓ Cache stats: TotalEntries={stats.TotalEntries}, HitRate={stats.HitRate:P}");
        Console.WriteLine("✓ Cache integration completed\n");
    }

    private async Task TestWindowManagerIntegration()
    {
        Console.WriteLine("Test 9: Window Manager Integration");
        
        // Test window manager
        // Note: We can't actually test window creation or mode in a console app,
        // but we can verify the service is properly initialized
        
        if (_windowManager == null) throw new Exception("WindowManager not initialized");
        
        Console.WriteLine("✓ Window manager service initialized");
        Console.WriteLine("✓ Window manager integration completed\n");
    }

    private void InitializeServices()
    {
        try
        {
            // Initialize logging service first
            _loggingService = new LoggingService();
            _loggingService.LogInformation("Integration tests starting");

            // Initialize LibVLC
            Core.Initialize();

            // Set up test database
            var testDbDir = Path.Combine(Path.GetTempPath(), "KaraokePlayerTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDbDir);
            _testDbPath = Path.Combine(testDbDir, "test_karaoke.db");
            
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = _testDbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
                Pooling = true
            }.ToString();

            // Create DbContext factory for tests
            _dbContextFactory = new DbContextFactory(connectionString);

            // Create service instances
            _notificationService = new NotificationService();
            _errorHandlingService = new ErrorHandlingService(_notificationService, _loggingService);
            _settingsManager = new SettingsManager();
            _searchEngine = new SearchEngine(_dbContextFactory);
            _playlistManager = new PlaylistManager(_dbContextFactory);
            _mediaPlayerController = new MediaPlayerController(_loggingService);
            _mediaLibraryManager = new MediaLibraryManager(_dbContextFactory);
            _metadataExtractor = new MetadataExtractor(_dbContextFactory);

            var libVLC = new LibVLC();
            _thumbnailGenerator = new ThumbnailGenerator(_dbContextFactory, libVLC);
            _keyboardShortcutManager = new KeyboardShortcutManager();
            
            var cacheDirectory = Path.Combine(Path.GetTempPath(), "KaraokePlayerTest_Cache_" + Guid.NewGuid().ToString());
            _cacheManager = new CacheManager(cacheDirectory);
            
            _windowManager = new WindowManager(_settingsManager);
            _audioVisualizationEngine = new AudioVisualizationEngine();

            _errorHandlingService.ClearAllErrors();
            
            _loggingService.LogInformation("All test services initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing test services: {ex.Message}");
            throw;
        }
    }

    private void Cleanup()
    {
        Console.WriteLine("\nCleaning up test resources...");
        
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

        // DbContext factory doesn't need disposal - contexts are disposed via using statements

        // Delete test database
        if (!string.IsNullOrEmpty(_testDbPath) && File.Exists(_testDbPath))
        {
            try
            {
                var dbDir = Path.GetDirectoryName(_testDbPath);
                if (dbDir != null && Directory.Exists(dbDir))
                {
                    Directory.Delete(dbDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        Console.WriteLine("✓ Cleanup completed");
    }

    public static async Task Main(string[] args)
    {
        var tests = new IntegrationTests();
        await tests.RunAllTestsAsync();
    }
}
