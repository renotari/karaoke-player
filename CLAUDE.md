# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Karaoke Player is a professional Windows karaoke and media player built with .NET 8 and Avalonia UI. It supports multi-format playback (MP4, MKV, WEBM, MP3), features crossfade transitions, audio visualizations, and flexible single/dual-screen display modes optimized for large media libraries (30,000+ files).

**Technology Stack:**
- Framework: .NET 8 (C#)
- UI: Avalonia UI 11.3 (MVVM pattern with ReactiveUI)
- Media Engine: LibVLCSharp 3.9
- Database: SQLite with Entity Framework Core 9.0
- Graphics: SkiaSharp 3.119
- Metadata: TagLib#, MediaInfo.Wrapper

## Build and Development Commands

### Building the Application

```bash
# Restore dependencies
dotnet restore

# Build debug version
dotnet build

# Build release version
dotnet build --configuration Release

# Publish self-contained Windows x64 build
dotnet publish -c Release -r win-x64 --self-contained

# Using the build scripts (Windows)
build-release.bat          # Builds release version
build-installer.bat        # Creates installer (requires Inno Setup)
```

### Running Tests

The project includes comprehensive unit and integration tests embedded in the codebase:

```bash
# Run all unit tests (Task 31)
dotnet run -- --unit-tests

# Run integration tests (Task 28)
dotnet run -- --test
```

**Note:** Tests are implemented as separate runner classes (e.g., `RunUnitTests.cs`, `RunIntegrationTests.cs`) rather than using a traditional test framework. Test files follow naming patterns: `*Test.cs` (unit tests), `Verify*.cs`, `Test*.cs` (integration tests).

### Running the Application

```bash
# Standard run
dotnet run

# Run with specific arguments
dotnet run -- --test         # Test mode
dotnet run -- --unit-tests   # Unit test mode
```

## Architecture and Code Structure

### Application Entry Points

**Program.cs**: Main entry point that handles command-line arguments and initializes Avalonia. Supports `--test` and `--unit-tests` flags for running test suites.

**App.axaml.cs**: Application lifecycle manager that:
- Initializes all services in dependency injection pattern
- Configures SQLite database with performance optimizations (WAL mode, shared cache, connection pooling)
- Sets up LibVLC for media playback
- Manages first-run welcome dialog flow
- Handles application shutdown and resource cleanup

### Service Architecture (Dependency Injection Pattern)

All services follow interface-based design located in the `Services/` directory:

**Core Services:**
- `IMediaPlayerController` / `MediaPlayerController`: Manages LibVLC playback, crossfade transitions, audio/video control
- `IMediaLibraryManager` / `MediaLibraryManager`: Scans directories, monitors file changes, manages media database
- `IPlaylistManager` / `PlaylistManager`: Handles playlist operations, persistence, M3U import/export
- `ISearchEngine` / `SearchEngine`: Optimized search across large catalogs with partial matching and history

**Media Processing:**
- `IMetadataExtractor` / `MetadataExtractor`: Extracts metadata using TagLib# and MediaInfo
- `IThumbnailGenerator` / `ThumbnailGenerator`: Generates video thumbnails using LibVLC
- `ICacheManager` / `CacheManager`: Manages thumbnail and metadata caching

**UI Services:**
- `IWindowManager` / `WindowManager`: Manages single/dual screen modes and window lifecycle
- `INotificationService` / `NotificationService`: Toast notification system
- `IKeyboardShortcutManager` / `KeyboardShortcutManager`: Global keyboard shortcut handling
- `IAudioVisualizationEngine` / `AudioVisualizationEngine`: Audio visualizations using SkiaSharp

**Infrastructure:**
- `ISettingsManager` / `SettingsManager`: Persists user settings to JSON
- `ILoggingService` / `LoggingService`: Application logging to files
- `IErrorHandlingService` / `ErrorHandlingService`: Centralized error handling and recovery

Services are instantiated in `App.axaml.cs.InitializeServices()` and injected into ViewModels.

### MVVM Architecture

**Models** (`Models/`):
- `MediaFile`: Core media entity with file info, metadata, thumbnail paths
- `PlaylistItem`: Playlist entry with position and metadata
- `AppSettings`: Application settings data model
- `KaraokeDbContext`: EF Core DbContext with performance indexes
- `MediaError`, `ToastNotification`, `SearchHistory`: Supporting models

**ViewModels** (`ViewModels/`):
- `MainWindowViewModel`: Primary VM for single-screen mode, coordinates all features
- `PlaybackWindowViewModel`: VM for dual-screen playback window
- `SettingsViewModel`: Settings management UI
- `PlaylistComposerViewModel`: Playlist composition tool
- `PlaylistItemViewModel`: Individual playlist item representation
- All VMs inherit from `ViewModelBase` and use ReactiveUI for property change notifications

**Views** (`Views/`):
- `MainWindow`: Main single-screen interface
- `PlaybackWindow`: Dual-screen playback window
- `ControlWindow`: Dual-screen control window
- `SettingsWindow`: Settings dialog
- `PlaylistComposerWindow`: Playlist composition tool
- `WelcomeDialog`: First-run setup dialog
- Custom controls: `PlaylistItemControl`, `ToastNotificationControl`, `ToastNotificationContainer`

### Database and Entity Framework

**Database Location:** `%APPDATA%\KaraokePlayer\karaoke.db`

**Migrations:** Located in `Models/Migrations/`
- Initial schema: `20251105191301_InitialCreate`
- Performance indexes: `20251111200852_AddPerformanceIndexes`

**Performance Optimizations:**
- WAL (Write-Ahead Logging) mode enabled
- Connection pooling and shared cache
- Indexes on `FilePath`, `Artist`, `Title`, `FileName` for fast search
- 64MB cache size, memory temp storage

**Creating Migrations:**
```bash
# Add new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Generate SQL script
dotnet ef migrations script
```

**Note:** `KaraokeDbContextFactory` provides design-time support for EF Core tooling.

### LibVLC Integration

LibVLC binaries are automatically copied to output via `.csproj` configuration:
- Native libraries located in `libvlc/` subdirectory of output
- Must NOT use `PublishSingleFile=true` due to LibVLC native dependencies
- Core.Initialize() called in `App.axaml.cs` before any media operations

## Key Implementation Patterns

### MVVM Framework - Hybrid Approach

The project uses a **hybrid MVVM approach** combining:
- **CommunityToolkit.Mvvm** for properties and commands (primary framework)
- **ReactiveUI** for throttled search functionality only (minimal usage)

**CommunityToolkit.Mvvm Features Used:**
- `[ObservableProperty]` source generators for automatic property generation
- `[RelayCommand]` attributes for command generation with CanExecute support
- `ObservableObject` base class for property change notifications

**ReactiveUI Features Used (Limited):**
- `WhenAnyValue().Throttle()` for search query throttling (3 instances)
- MessageBus for cross-window communication (PlaybackWindowViewModel only)

**Example - Property Declaration:**
```csharp
[ObservableProperty]
private string _searchQuery = string.Empty;
// Auto-generates: public string SearchQuery { get; set; } with INotifyPropertyChanged
```

**Example - Command Declaration:**
```csharp
[RelayCommand]
private async Task Play()
{
    // Command implementation
}
// Auto-generates: public IAsyncRelayCommand PlayCommand { get; }
```

**Example - Throttled Search (ReactiveUI):**
```csharp
var throttledSearch = this.WhenAnyValue(x => x.SearchQuery)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(async query => await PerformSearch(query));
_disposables.Add(throttledSearch); // Proper disposal management
```

### Background Processing Pattern

Metadata extraction and thumbnail generation use producer-consumer queues:
- `QueueForExtraction()` / `QueueForGeneration()` add items to queue
- `StartProcessing()` begins background processing
- `StopProcessing()` cleanly shuts down workers

### Window Management Modes

The application supports two primary display modes managed by `WindowManager`:

**Single Screen Mode:**
- Main window contains video player, catalog, and playlist
- Can toggle to "Video Mode" for maximized video with collapsible control handle
- Default mode for single-monitor setups

**Dual Screen Mode:**
- Playback window (full-screen video) on secondary monitor
- Control window (catalog + playlist) on primary monitor
- Activated via `Ctrl+D` or Settings

### Crossfade Implementation

Crossfade uses dual LibVLC MediaPlayer instances:
- Primary player for current track
- Secondary player for next track
- Synchronized volume fade over configurable duration (1-20 seconds)
- Video crossfade achieved through opacity transitions

## File Locations and Configuration

### User Data Directories

- **Application Data:** `%APPDATA%\KaraokePlayer\`
- **Settings:** `%APPDATA%\KaraokePlayer\settings.json`
- **Database:** `%APPDATA%\KaraokePlayer\karaoke.db`
- **Cache:** `%APPDATA%\KaraokePlayer\cache\`
- **Logs:** `%APPDATA%\KaraokePlayer\logs\`
- **Playlists:** `%APPDATA%\KaraokePlayer\current-playlist.json`

### Assets

- **Application Icon:** `Assets\avalonia-logo.ico`
- **Other assets:** `Assets\**` (included as AvaloniaResource)

## Common Development Patterns

### Adding a New Service

1. Create interface in `Services/IYourService.cs`
2. Implement in `Services/YourService.cs`
3. Add unit test class in `Services/YourServiceTest.cs`
4. Register in `App.axaml.cs.InitializeServices()`
5. Inject into ViewModels as needed

### Adding a New Window/Dialog

1. Create AXAML in `Views/YourWindow.axaml`
2. Create code-behind in `Views/YourWindow.axaml.cs`
3. Create ViewModel in `ViewModels/YourWindowViewModel.cs`
4. Use `WindowManager` for window lifecycle or show directly with `ShowDialog()`

### Adding Database Entities

1. Add model class in `Models/YourEntity.cs`
2. Add DbSet to `KaraokeDbContext`
3. Create migration: `dotnet ef migrations add AddYourEntity`
4. Update database: `dotnet ef database update`
5. Add corresponding service methods for CRUD operations

### Working with Media Files

Always access media files through `IMediaLibraryManager` to ensure:
- Database consistency
- Metadata is loaded
- Thumbnails are generated
- File system monitoring is active

## Performance Considerations

The application is optimized for large libraries (30,000+ files):

- **Search Speed:** < 300ms for searches across entire catalog
- **UI Responsiveness:** < 100ms for all interactions
- **Startup Time:** < 2 seconds
- **Memory Usage:** < 300MB with 10,000 files loaded

**Optimization Techniques:**
- Lazy loading of thumbnails via `LazyThumbnailLoader`
- Database indexes on frequently queried fields
- Background processing for heavy operations
- Connection pooling and SQLite optimizations
- PerformanceMonitor class for tracking bottlenecks

## Testing Strategy

Tests are organized as standalone runner classes rather than using xUnit/NUnit:

**Unit Tests** (Task 31):
- Located in `Services/*Test.cs` files
- Run via `RunUnitTests.cs` orchestrator
- Each service has corresponding test file
- Tests use in-memory database where applicable

**Integration Tests** (Task 28):
- Located in `IntegrationTests.cs`
- Run via `RunIntegrationTests.cs` orchestrator
- Test end-to-end scenarios across multiple services
- Verify UI integration and window lifecycle

**Verification Tests:**
- Files named `Verify*.cs` test specific features
- Files named `Test*.cs` test individual components

When adding tests, follow existing patterns and register in the appropriate test runner.

## Installer and Deployment

**Installer Script:** `Setup.iss` (Inno Setup format)
- Creates Windows installer with .NET 8 runtime check
- Installs to `C:\Program Files\Karaoke Player`
- Creates desktop and start menu shortcuts
- Handles application data directory setup

**Build Process:**
1. Run `build-release.bat` to create release build
2. Run `build-installer.bat` to generate installer (requires Inno Setup)
3. Output: `installer\KaraokePlayer-Setup-1.0.0.exe`

**Version Updates:** When bumping version, update:
- `KaraokePlayer.csproj` (Version, AssemblyVersion, FileVersion)
- `Setup.iss` (MyAppVersion)
- `README.md` (version badge)

See `docs/DEPLOYMENT.md` for comprehensive deployment guide.

## Debugging Tips

**Avalonia DevTools:** Press F12 in Debug build to open Avalonia DevTools for UI inspection and debugging.

**Logging:** All services use `ILoggingService` - check logs at `%APPDATA%\KaraokePlayer\logs\` for detailed diagnostics.

**Common Issues:**
- LibVLC playback issues: Ensure `libvlc/` folder exists in output directory with all native DLLs
- Database lock errors: Check WAL mode is enabled, use connection pooling
- UI freezing: Ensure heavy operations run on background threads, not UI thread
- Settings not persisting: Verify `%APPDATA%\KaraokePlayer\` directory has write permissions

## Additional Documentation

- **Quick Start:** `docs/QUICKSTART.md`
- **Deployment Guide:** `docs/DEPLOYMENT.md`
- **Manual Testing:** `docs/MANUAL_TESTING_GUIDE.md`
- **Unit Tests Summary:** `docs/UNIT_TESTS_TASK31_SUMMARY.md`
- **Integration Tests Summary:** `docs/INTEGRATION_TESTS_SUMMARY.md`
