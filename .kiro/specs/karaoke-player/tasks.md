# Implementation Plan

This implementation plan breaks down the karaoke player design into discrete coding tasks. Each task builds incrementally on previous work, ending with a fully integrated application.

## Task Execution Notes

- All tasks are required for a comprehensive implementation
- Each task references specific requirements from requirements.md
- All context documents (requirements.md, design.md) should be available during implementation
- Tasks build incrementally - complete in order for best results

---

- [x] 1. Set up project structure and development environment






  - Initialize .NET 8 solution with Avalonia UI project template
  - Add NuGet packages: LibVLCSharp, LibVLCSharp.Avalonia, Entity Framework Core, SQLite, TagLib#, SkiaSharp, ReactiveUI
  - Configure project for Windows target (primary), with cross-platform structure
  - Set up MVVM folder structure: Models, ViewModels, Views, Services
  - Create basic App.axaml and MainWindow.axaml
  - _Requirements: Foundation for all requirements_
- [x] 2. Implement core data models and database schema









- [x] 2. Implement core data models and database schema

  - Create MediaFile model with properties (id, filePath, filename, type, format, metadata, thumbnailPath, etc.)
  - Create MediaMetadata model (duration, artist, title, album, resolution, fileSize, hasSubtitles)
  - Create PlaylistItem model (mediaFile, addedAt, isDuplicate, error)
  - Create AppSettings model (mediaDirectory, displayMode, volume, crossfade settings, etc.)
  - Set up SQLite DbContext with Entity Framework Core
  - Create database migrations for media library schema
  - _Requirements: 1, 1A, 1B, 3, 10_

- [x] 3. Implement Settings Manager service





  - Create ISettingsManager interface and SettingsManager implementation
  - Implement load/save settings from JSON file in user data directory
  - Implement getSetting/setSetting with type safety
  - Implement resetToDefaults with default configuration values
  - Add validation for settings values (e.g., crossfade duration 1-20 seconds)
  - Wire up settings to ReactiveUI property change notifications
  - _Requirements: 10_

- [x] 4. Implement Media Library Manager service




  - Create IMediaLibraryManager interface and MediaLibraryManager implementation
  - Implement scanDirectory to recursively find MP4, MKV, WEBM, MP3 files
  - Store file paths and basic info in SQLite database
  - Implement FileSystemWatcher integration for auto-refresh
  - Implement events: FilesAdded, FilesRemoved, FilesModified
  - Implement getMediaFiles to query from database
  - Add progress reporting for directory scanning
  - _Requirements: 1, 13_

- [x] 5. Implement Metadata Extractor service





  - Create IMetadataExtractor interface and MetadataExtractor implementation
  - Integrate TagLib# for MP3 ID3 tag extraction (artist, title, album, artwork)
  - Integrate FFmpeg.NET or MediaInfo.NET for video metadata (duration, resolution, fileSize)
  - Implement filename parsing with multiple patterns: "Artist - Title", "Artist-Title", "Title (Artist)", "Artist_Title"
  - Implement background queue processing for metadata extraction
  - Update database with extracted metadata
  - Handle extraction failures gracefully (use filename as fallback)
  - _Requirements: 1A_

- [x] 6. Implement Thumbnail Generator service





  - Create IThumbnailGenerator interface and ThumbnailGenerator implementation
  - Use SkiaSharp to generate video thumbnails (capture frame at 10% duration)
  - Extract embedded artwork from MP3 files using TagLib#
  - Create default placeholder thumbnails for files without artwork
  - Implement background queue processing for thumbnail generation
  - Store thumbnails in cache directory with file ID naming
  - Update database with thumbnail paths
  - _Requirements: 1B_

- [ ] 7. Implement Cache Manager service
  - Create ICacheManager interface and CacheManager implementation
  - Implement set/get/invalidate methods for thumbnails, metadata, search index
  - Subscribe to MediaLibraryManager file change events for cache invalidation
  - Implement LRU eviction policy with 500MB max size for thumbnails
  - Persist cache index to disk for fast startup
  - Implement getCacheStats for monitoring
  - _Requirements: Performance optimization for 1, 1A, 1B_

- [ ] 8. Implement Search Engine service
  - Create ISearchEngine interface and SearchEngine implementation
  - Index media files in SQLite with full-text search on artist, title, filename
  - Implement search method with partial matching using LIKE queries
  - Implement search history storage (last 10 searches)
  - Ensure search results return within 300ms for 30K files
  - Add search result ranking by relevance
  - _Requirements: 2, 15, 21_

- [ ] 9. Implement Playlist Manager service
  - Create IPlaylistManager interface and PlaylistManager implementation
  - Implement addSong with position ('next' or 'end')
  - Implement removeSong, reorderSong, clearPlaylist, shufflePlaylist
  - Implement duplicate detection (isDuplicate check)
  - Implement auto-save current playlist to JSON (debounced 1 second)
  - Implement restoreLastSession to load playlist on startup
  - Implement M3U/M3U8 export (savePlaylist) and import (loadPlaylist)
  - Use ReactiveUI ObservableCollection for reactive playlist updates
  - _Requirements: 3, 9, 14, 16_

- [ ] 10. Implement Media Player Controller with LibVLC
  - Create IMediaPlayerController interface and MediaPlayerController implementation
  - Initialize LibVLC with two MediaPlayer instances (current and preloaded)
  - Implement play, pause, stop, seek methods
  - Implement setVolume and audio device selection (setAudioDevice)
  - Implement subtitle toggle (toggleSubtitles)
  - Wire up LibVLC events for playback state changes
  - Implement getAudioSpectrum using LibVLC audio callbacks
  - Handle playback errors and skip to next song on failure
  - _Requirements: 4, 7, 11, 22_

- [ ] 11. Implement crossfade functionality in Media Player Controller
  - Implement enableCrossfade with configurable duration (1-20 seconds)
  - Implement preloadNext to load next song in second MediaPlayer instance
  - Calculate crossfade trigger point: currentDuration - crossfadeDuration
  - Implement volume ramping using LibVLC volume control (fade out current, fade in next)
  - Coordinate video opacity transitions with Avalonia animations (dip-to-black)
  - Handle crossfade cancellation if next song fails to load
  - Ensure seamless audio/video transition without stuttering
  - _Requirements: 8_

- [ ] 12. Implement Audio Visualization Engine
  - Create IAudioVisualizationEngine interface and AudioVisualizationEngine implementation
  - Use SkiaSharp for hardware-accelerated canvas rendering
  - Implement 4 visualization styles: bars, waveform, circular, particles
  - Connect to Media Player Controller's audio spectrum data
  - Implement real-time rendering at 30+ FPS using Avalonia's rendering loop
  - Add style selection and configuration
  - Display song title, artist, and artwork as background elements
  - _Requirements: 4, 17_

- [ ] 13. Implement Window Manager service
  - Create IWindowManager interface and WindowManager implementation
  - Implement setMode to switch between 'single' and 'dual' screen modes
  - Implement openPlaylistComposer to create Playlist Composer window
  - Implement toggleFullscreen for any window
  - Implement saveWindowState and restoreWindowState (position, size)
  - Set up message bus for cross-window communication using ReactiveUI MessageBus
  - Implement state synchronization across windows
  - _Requirements: 5, 6, 12_

- [ ] 14. Create Main Window UI (Single Screen Mode - Normal View)
  - Design MainWindow.axaml with three-pane layout: Catalog, Video, Playlist
  - Implement resizable split panes using Avalonia Grid with GridSplitter
  - Create search bar with real-time filtering in catalog pane
  - Create media catalog list with virtualization (VirtualizingStackPanel)
  - Display thumbnails, artist, title, duration for each media item
  - Create playlist pane with reorderable list (drag-and-drop support)
  - Add playback controls: play/pause, skip, volume slider, progress bar
  - Add status bar with current song info and settings indicators
  - Wire up to ViewModels with ReactiveUI bindings
  - _Requirements: 2, 3, 4, 5_

- [ ] 15. Create Main Window ViewModel
  - Create MainWindowViewModel with ReactiveUI
  - Implement properties: MediaFiles, SearchQuery, CurrentPlaylist, IsPlaying, CurrentSong
  - Implement commands: SearchCommand, AddToPlaylistCommand, RemoveSongCommand, PlayCommand, PauseCommand
  - Wire up to Search Engine, Playlist Manager, Media Player Controller services
  - Implement reactive property bindings for real-time UI updates
  - Handle optimistic updates for playlist operations (< 50ms UI response)
  - _Requirements: 2, 3, 4, 5_

- [ ] 16. Implement Video Mode (Maximized View) in Main Window
  - Add Video_Mode state to MainWindowViewModel
  - Create collapsible Control_Handle at bottom edge of video area
  - Implement handle expand/collapse on hover/click with auto-collapse after 3 seconds
  - Add Quick_Search interface in expanded handle
  - Add playlist view access and settings access from handle
  - Implement toggle between normal mode and Video_Mode
  - Ensure keyboard shortcuts remain active when handle is collapsed
  - _Requirements: 5A_

- [ ] 17. Create Playback Window UI (Dual Screen Mode)
  - Design PlaybackWindow.axaml with full-screen video display area
  - Embed LibVLC video output using LibVLCSharp.Avalonia VideoView control
  - Add subtitle display area at bottom
  - Implement minimal UI (no visible controls during playback)
  - Support fullscreen mode (F11 key)
  - Handle aspect ratio preservation for video content
  - Display audio visualizations for MP3 files
  - _Requirements: 4, 6, 12, 17_

- [ ] 18. Create Playback Window ViewModel
  - Create PlaybackWindowViewModel with ReactiveUI
  - Implement properties: CurrentSong, IsPlaying, CurrentTime, Duration, SubtitlesEnabled
  - Subscribe to Media Player Controller state changes via message bus
  - Implement fullscreen toggle command
  - Handle window-specific keyboard shortcuts
  - _Requirements: 4, 6, 12_

- [ ] 19. Create Control Window UI (Dual Screen Mode)
  - Design ControlWindow.axaml with catalog and playlist panes
  - Reuse catalog and playlist components from Main Window
  - Add search interface and playback controls
  - Add settings access and mode toggle
  - Wire up to same ViewModels as Main Window
  - _Requirements: 6_

- [ ] 20. Create Playlist Composer Window UI
  - Design PlaylistComposerWindow.axaml with two-pane layout
  - Create Catalog View (left pane) with search/filter box and artist filter dropdown
  - Implement multi-select support (Ctrl+Click, Shift+Click) in catalog list
  - Create Composition View (right pane) with reorderable playlist
  - Add playlist name input field
  - Implement drag-and-drop between catalog and composition panes
  - Add action buttons: Add Selected, Clear All, Shuffle, Load M3U, Save M3U, Save & Load for Play
  - Display total playlist duration
  - _Requirements: 23_

- [ ] 21. Create Playlist Composer ViewModel
  - Create PlaylistComposerViewModel with ReactiveUI
  - Implement properties: CatalogFiles, FilteredCatalogFiles, ComposedPlaylist, PlaylistName, SelectedCatalogItems
  - Implement commands: AddSelectedCommand, RemoveCommand, ReorderCommand, ClearCommand, ShuffleCommand
  - Implement SavePlaylistCommand (export to M3U file)
  - Implement LoadPlaylistCommand (import from M3U file for editing)
  - Implement SaveAndLoadForPlayCommand (save and replace current playing playlist)
  - Implement filtering by artist, title, metadata
  - _Requirements: 23_

- [ ] 22. Create Settings Window UI
  - Design SettingsWindow.axaml with tabbed interface (General, Audio, Display, Keyboard)
  - General tab: Media directory picker, display mode toggle, auto-play, shuffle
  - Audio tab: Volume, audio boost, audio output device selector, test audio button
  - Display tab: Theme selection, font size, visualization style, window layout preferences
  - Keyboard tab: Customizable keyboard shortcuts with conflict detection
  - Add crossfade settings: enable/disable, duration slider (1-20 seconds)
  - Add performance settings: preload buffer size, cache size
  - Add Reset to Defaults button and Apply/Cancel buttons
  - _Requirements: 10, 11, 22_

- [ ] 23. Create Settings ViewModel
  - Create SettingsViewModel with ReactiveUI
  - Bind all settings properties to Settings Manager
  - Implement validation for settings values
  - Implement real-time preview of changes where applicable
  - Implement ResetToDefaultsCommand
  - Implement ApplyCommand and CancelCommand
  - Detect and list available audio output devices
  - _Requirements: 10, 11, 22_

- [ ] 24. Implement keyboard shortcuts and navigation
  - Set up global keyboard shortcut handler in App.axaml.cs
  - Implement all shortcuts from design: Space (play/pause), arrows (skip/volume), F (fullscreen), etc.
  - Implement Tab navigation between UI elements
  - Add visual focus indicators for keyboard navigation
  - Implement Escape key to close dialogs and exit fullscreen
  - Make shortcuts customizable via Settings
  - _Requirements: 4, 12, 19_

- [ ] 25. Implement error handling and user notifications
  - Create error indicator UI components for playlist items
  - Implement toast notification system for system errors (bottom-right)
  - Handle corrupted file errors: skip to next song, mark with error indicator
  - Handle missing file errors: continue from buffer, mark with error indicator
  - Handle permission errors: mark files with error indicator
  - Handle crossfade failures: cancel and skip to next valid song
  - Implement duplicate song indicators in search results and playlist
  - Clear error states on application restart
  - _Requirements: 18_

- [ ] 26. Implement logging system
  - Create logging service using standard .NET ILogger
  - Configure file logging to application data directory
  - Log errors, file load failures, system issues with timestamp and severity
  - Implement log file rotation when size limit reached
  - Log playback events, crossfade transitions, and errors
  - _Requirements: 20_

- [ ] 27. Implement first-run experience and initialization
  - Detect first run (no settings file exists)
  - Show welcome dialog with media directory selection
  - Default to Windows user media directory if not specified
  - Perform initial directory scan with progress indicator
  - Start background metadata and thumbnail processing
  - Show tips for using the application
  - _Requirements: 1_

- [ ] 28. Implement performance optimizations
  - Enable virtualization for all long lists (catalog, playlist, search results)
  - Implement lazy loading for thumbnails (load as scrolled into view)
  - Debounce search input (300ms delay)
  - Throttle scroll events for performance
  - Optimize SQLite queries with proper indexes
  - Implement connection pooling for database access
  - Profile and optimize for 30K file library support
  - _Requirements: 21_

- [ ] 29. Wire up all components and test integration
  - Connect all services via dependency injection in App.axaml.cs
  - Wire up ViewModels to Services
  - Test single screen mode (normal and video mode)
  - Test dual screen mode with separate windows
  - Test playlist composer workflow
  - Test crossfade transitions between songs
  - Test error handling scenarios
  - Test with large library (10K+ files)
  - _Requirements: All_

- [ ] 30. Implement application packaging and deployment
  - Configure Avalonia build for Windows executable
  - Bundle LibVLC native binaries with application
  - Create installer using WiX or Inno Setup
  - Set up application icon and metadata
  - Test installation on clean Windows system
  - Create user documentation (README)
  - _Requirements: Deployment_

- [ ] 31. Write unit tests for core services
  - Test Metadata Extractor filename parsing patterns
  - Test Playlist Manager operations (add, remove, reorder, shuffle)
  - Test Search Engine query performance and accuracy
  - Test Settings Manager validation logic
  - Test Cache Manager eviction policy
  - _Requirements: Testing_

- [ ] 32. Write integration tests
  - Test Media Library Manager with FileSystemWatcher
  - Test crossfade transitions with LibVLC
  - Test M3U playlist save/load
  - Test multi-window state synchronization
  - Test error recovery scenarios
  - _Requirements: Testing_

- [ ] 33. Performance testing and optimization
  - Benchmark search performance with 30K files (target < 300ms)
  - Benchmark UI responsiveness (target < 100ms)
  - Benchmark startup time (target < 2 seconds)
  - Test memory usage with 10K files loaded (target < 300MB)
  - Profile and optimize bottlenecks
  - _Requirements: 21_

