# Design Document

## Overview

The Karaoke Player is a Windows desktop application built to provide professional karaoke and media playback experiences. The system supports multiple media formats (MP4, MKV, WEBM, MP3) with advanced features including crossfade transitions, audio visualizations, and flexible single/dual-screen configurations. The architecture prioritizes performance for large libraries (up to 30,000 files) while maintaining responsive UI and smooth playback.

## Architecture

### Performance Targets

The application must meet these performance requirements:

- **Library Size**: Support up to 30,000 media files
- **Initial Scan**: Complete directory scan as fast as hardware allows (target < 5 seconds on SSD, metadata loaded in background)
- **Search Response**: Return results in < 300ms for any query (SQLite indexed queries)
- **UI Responsiveness**: All user interactions provide feedback in < 100ms
- **Playlist Operations**: UI updates in < 50ms with reactive bindings
- **Video Playback**: Maintain smooth 30+ FPS during playback (hardware-accelerated)
- **Crossfade Transition**: Seamless audio/video transition using LibVLC dual player instances
- **Memory Usage**: Target < 300MB RAM with 10,000 files loaded (.NET baseline ~50-80MB)
- **Startup Time**: Launch to ready state in < 2 seconds

**Performance Advantages of .NET Stack:**
- Native performance with low memory footprint
- LibVLC provides professional-grade video crossfade (frame-perfect transitions)
- SQLite with EF Core enables fast indexed searches
- Hardware-accelerated rendering via Skia
- Efficient file system monitoring with FileSystemWatcher

### High-Level Architecture

The application follows a modular architecture with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                     Presentation Layer                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Main Window  │  │ Playback     │  │  Playlist    │      │
│  │              │  │ Window       │  │  Composer    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            │
┌─────────────────────────────────────────────────────────────┐
│                     Application Layer                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Playlist     │  │ Search       │  │  Settings    │      │
│  │ Manager      │  │ Engine       │  │  Manager     │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            │
┌─────────────────────────────────────────────────────────────┐
│                      Media Layer                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Media        │  │ Metadata     │  │  Thumbnail   │      │
│  │ Player       │  │ Extractor    │  │  Generator   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            │
┌─────────────────────────────────────────────────────────────┐
│                      Storage Layer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ File System  │  │ Cache        │  │  Config      │      │
│  │ Monitor      │  │ Manager      │  │  Storage     │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

**Selected Stack: .NET 8 + LibVLCSharp + Avalonia UI**

This stack provides the best balance of performance, reliability, and cross-platform capability.

**Framework**: .NET 8 (C#)
- Cross-platform desktop application (Windows, macOS, Linux)
- Low memory footprint (~50-80MB baseline)
- Fast startup time (< 2 seconds)
- Mature, stable platform with long-term support

**Media Engine**: LibVLCSharp
- Official .NET binding for VLC media player
- **Proven crossfade capability**: VLC natively supports seamless transitions
- Hardware-accelerated video decoding
- Supports all required formats (MP4, MKV, WEBM, MP3)
- Built-in subtitle support
- Lower-level control for frame-perfect playback
- Dual media player instances for crossfade

**UI Framework**: Avalonia UI
- Modern, cross-platform XAML-based UI framework
- Similar to WPF but works on Windows, macOS, Linux
- Hardware-accelerated rendering via Skia
- MVVM pattern with reactive programming (ReactiveUI)
- Modern styling system with fluent design support
- Growing ecosystem with active development

**State Management**: ReactiveUI + MVVM
- Reactive property binding (INotifyPropertyChanged)
- Command pattern for user actions
- Observable collections for dynamic lists
- Cross-window state synchronization via message bus

**Database**: SQLite with Entity Framework Core
- Lightweight embedded database for media library
- Fast indexing and querying (< 300ms for 30K files)
- LINQ for type-safe queries
- Migrations for schema updates

**File System**: FileSystemWatcher (built-in .NET)
- Native file monitoring
- Efficient change detection
- Cross-platform support

**Metadata Extraction**:
- TagLib# for audio metadata (ID3 tags, album art)
- FFmpeg.NET or MediaInfo.NET for video metadata
- SkiaSharp for thumbnail generation (cross-platform)

**Audio Visualization**: Custom implementation with SkiaSharp
- Real-time audio spectrum analysis via LibVLC audio callbacks
- Hardware-accelerated canvas rendering
- 4 visualization styles: bars, waveform, circular, particles

**Advantages:**
- ✅ Guaranteed seamless video crossfade (VLC's native capability)
- ✅ Low memory usage (~50-80MB vs ~150MB for Electron)
- ✅ Fast startup (< 2 seconds vs 5 seconds)
- ✅ Native performance on all platforms
- ✅ Lower resource usage for 30K file libraries
- ✅ Cross-platform ready (Windows, macOS, Linux)
- ✅ Modern UI framework with active development

**Tradeoffs:**
- ⚠️ Longer development time than Electron/React
- ⚠️ Smaller developer pool familiar with Avalonia vs React
- ⚠️ Audio visualizations require custom implementation (no Web Audio API)
- ⚠️ Avalonia is newer/less mature than WPF (but actively developed)

## Components and Interfaces

**Note**: The component architecture below is stack-agnostic. Interfaces are shown in TypeScript-style pseudocode for readability. Implementation details will vary based on chosen stack:
- **Option A (.NET)**: Use C# interfaces, async/await, events, MVVM pattern
- **Option B (Electron)**: Use TypeScript interfaces, Promises, event emitters, React hooks

### 1. Media Library Manager

**Responsibilities:**
- Scan media directory for supported files
- Maintain indexed file list with paths
- Monitor directory for changes (auto-refresh via FileSystemWatcher/.NET or chokidar/Node.js)
- Coordinate metadata extraction and thumbnail generation
- Emit file change events to CacheManager for invalidation

**Key Methods:**
```typescript
interface MediaLibraryManager {
  scanDirectory(path: string): Promise<void>
  getMediaFiles(): MediaFile[]
  refreshLibrary(): Promise<void>
  onFilesAdded(callback: (files: MediaFile[]) => void): void
  onFilesRemoved(callback: (files: MediaFile[]) => void): void
  onFilesModified(callback: (files: MediaFile[]) => void): void
}
```

**Integration with CacheManager:**
- CacheManager subscribes to `onFilesAdded`, `onFilesRemoved`, `onFilesModified` events
- Ensures cache stays synchronized with file system changes

### 2. Metadata Extractor

**Responsibilities:**
- Extract metadata from video files (duration, resolution, artist, title)
- Extract ID3 tags from MP3 files
- Parse filenames using multiple patterns
- Process files in background queue

**Key Methods:**
```typescript
interface MetadataExtractor {
  extractVideoMetadata(filePath: string): Promise<VideoMetadata>
  extractAudioMetadata(filePath: string): Promise<AudioMetadata>
  parseFilename(filename: string): { artist: string, title: string }
  processQueue(): void
}
```

### 3. Thumbnail Generator

**Responsibilities:**
- Generate thumbnails from video files
- Extract artwork from MP3 files
- Create default thumbnails
- Cache generated thumbnails

**Key Methods:**
```typescript
interface ThumbnailGenerator {
  generateVideoThumbnail(filePath: string): Promise<string>
  extractAudioArtwork(filePath: string): Promise<string>
  createDefaultThumbnail(mediaType: string): string
  getCachedThumbnail(fileId: string): string | null
}
```

### 4. Playlist Manager

**Responsibilities:**
- Maintain current playlist queue
- Handle add/remove/reorder operations
- Auto-persist current playlist to app data directory
- Save/load M3U/M3U8 files to user-specified locations
- Track duplicate songs
- Restore last session playlist on startup

**Key Methods:**
```typescript
interface PlaylistManager {
  addSong(song: MediaFile, position: 'next' | 'end'): void
  removeSong(index: number): void
  reorderSong(fromIndex: number, toIndex: number): void
  clearPlaylist(): void
  shufflePlaylist(): void
  savePlaylist(filePath: string): Promise<void>  // Export to M3U/M3U8
  loadPlaylist(filePath: string): Promise<void>  // Import from M3U/M3U8
  getCurrentPlaylist(): MediaFile[]
  isDuplicate(song: MediaFile): boolean
  autoSaveCurrentPlaylist(): Promise<void>  // Internal auto-save
  restoreLastSession(): Promise<void>  // Load on startup
}
```

**Persistence Strategy:**
- **Current Queue**: Auto-saved to `userData/current-playlist.json` on every change (debounced 1 second)
- **Session Restore**: Automatically loads `current-playlist.json` on app startup
- **M3U Export/Import**: User-initiated save/load to any location for sharing playlists
- **Format**: Internal JSON format includes full metadata; M3U format for compatibility

### 5. Media Player Controller

**Responsibilities:**
- Control video/audio playback using LibVLC
- Manage automatic crossfade transitions between tracks
- Manage dual LibVLC MediaPlayer instances
- Route audio to selected output device
- Handle subtitle tracks
- Provide audio spectrum data for visualizations
- Support future audio filters/effects via LibVLC

**Key Methods:**
```typescript
interface MediaPlayerController {
  play(mediaFile: MediaFile): Promise<void>
  pause(): void
  stop(): void
  seek(time: number): void
  setVolume(level: number): void
  enableCrossfade(enabled: boolean, duration: number): void
  preloadNext(mediaFile: MediaFile): void
  setAudioDevice(deviceId: string): void
  toggleSubtitles(enabled: boolean): void
  getAudioSpectrum(): float[]  // For visualizations
  getCurrentPlayer(): LibVLC.MediaPlayer
  getPreloadedPlayer(): LibVLC.MediaPlayer | null
}
```

**Crossfade Implementation (LibVLC):**
- Manages two LibVLC MediaPlayer instances (current and next)
- LibVLC native volume control for smooth audio ramping
- Avalonia opacity animations for video fade effects
- Preloads next track when current track reaches trigger point: `currentTrackDuration - crossfadeDuration`
- Seamless transitions using LibVLC's professional-grade playback engine
- Audio spectrum callbacks for real-time visualization data

**Crossfade Advantages:**
- Frame-perfect video transitions (LibVLC handles at rendering level)
- No frame drops even on lower-end hardware
- Professional-grade crossfade matching hardware players
- Reliable subtitle synchronization during transitions

### 6. Search Engine

**Responsibilities:**
- Index media files for fast searching
- Support partial matching
- Filter by artist, title, filename
- Maintain search history

**Key Methods:**
```typescript
interface SearchEngine {
  search(query: string): MediaFile[]
  indexFiles(files: MediaFile[]): void
  addToHistory(query: string): void
  getHistory(): string[]
}
```

### 7. Audio Visualization Engine

**Responsibilities:**
- Analyze audio frequency spectrum
- Render music-reactive animations
- Support multiple visualization styles
- Maintain 30+ FPS performance

**Key Methods:**
```typescript
interface AudioVisualizationEngine {
  initialize(audioContext: AudioContext): void
  setStyle(style: VisualizationStyle): void
  render(canvas: HTMLCanvasElement): void
  updateAudioData(frequencyData: Uint8Array): void
}

type VisualizationStyle = 'bars' | 'waveform' | 'circular' | 'particles'
```

**Initial Visualization Styles:**

1. **Bars (Default)**
   - Classic frequency bars visualization
   - Vertical bars representing frequency spectrum
   - Color gradient based on amplitude
   - Smooth animation with decay

2. **Waveform**
   - Oscilloscope-style waveform display
   - Shows audio waveform in real-time
   - Centered with mirrored top/bottom
   - Glowing line effect

3. **Circular**
   - Radial frequency visualization
   - Bars emanate from center in circle
   - Rotates slowly for visual interest
   - Pulsing center based on bass frequencies

4. **Particles**
   - Particle system reacting to audio
   - Particles move and change size with beat
   - Color shifts based on frequency ranges
   - More CPU intensive, optional for lower-end systems

### 8. Window Manager

**Responsibilities:**
- Manage single/dual screen modes
- Handle window positioning and sizing
- Persist window states
- Coordinate fullscreen mode
- Manage IPC communication between windows

**Key Methods:**
```typescript
interface WindowManager {
  setMode(mode: 'single' | 'dual'): void
  openPlaylistComposer(): void
  toggleFullscreen(windowId: string): void
  saveWindowState(): void
  restoreWindowState(): void
  sendToWindow(windowId: string, channel: string, data: any): void
  broadcastToAll(channel: string, data: any): void
}
```

**Inter-Process Communication (IPC):**

Electron's IPC mechanism handles communication between windows:

**Main Process → Renderer Windows:**
- `playback:start` - Notify playback window to start video
- `playback:stop` - Stop current playback
- `playlist:updated` - Sync playlist changes across windows
- `settings:changed` - Apply settings changes in all windows

**Renderer Windows → Main Process:**
- `playlist:add` - Add song from any window
- `playlist:remove` - Remove song from playlist
- `playback:control` - Play/pause/skip commands
- `window:request-state` - Request current app state

**State Synchronization:**
- Zustand store in main process acts as single source of truth
- Renderer windows maintain local Zustand stores for immediate UI updates
- **Optimistic Updates**: UI updates immediately on user action (< 50ms)
- State mutations sent to main process asynchronously for persistence and broadcast
- Conflict resolution: Main process broadcasts authoritative state, renderers reconcile
- Rollback on failure: If main process rejects, renderer reverts optimistic change

### 9. Settings Manager

**Responsibilities:**
- Store and retrieve application settings
- Validate configuration values
- Apply settings changes in real-time
- Handle configuration profiles

**Key Methods:**
```typescript
interface SettingsManager {
  getSetting<T>(key: string): T
  setSetting<T>(key: string, value: T): void
  loadSettings(): Promise<void>
  saveSettings(): Promise<void>
  resetToDefaults(): void
}
```

### 10. Cache Manager

**Responsibilities:**
- Cache thumbnails, metadata, and search indices
- Manage cache size and eviction policy
- Persist cache to disk
- Invalidate stale cache entries
- Provide cache statistics

**Key Methods:**
```typescript
interface CacheManager {
  set(key: string, value: any, category: 'thumbnail' | 'metadata' | 'search'): void
  get(key: string, category: string): any | null
  invalidate(key: string, category: string): void
  invalidateAll(category: string): void
  clear(): void
  getCacheStats(): { size: number, hitRate: number, categories: Record<string, number> }
  pruneCache(): void  // Remove least recently used items if over size limit
}
```

**Cache Strategy:**
- **Thumbnails**: Stored as base64 or file paths, LRU eviction, max 500MB
- **Metadata**: Stored as JSON objects, invalidated on file modification
- **Search Index**: Rebuilt when library changes, persisted between sessions
- **Persistence**: Cache stored in user data directory, loaded on startup
- **Invalidation**: 
  - Listens to MediaLibraryManager file change events (via chokidar)
  - On file added/modified: invalidate metadata and thumbnail for that file
  - On file removed: remove from cache
  - Compares file modification time on cache hit as secondary check
  - Manual refresh option clears all cache

## Data Models

### MediaFile
```typescript
interface MediaFile {
  id: string
  filePath: string
  filename: string
  type: 'video' | 'audio'
  format: 'mp4' | 'mkv' | 'webm' | 'mp3'
  metadata: MediaMetadata
  thumbnailPath: string | null
  metadataLoaded: boolean
  thumbnailLoaded: boolean
  error: MediaError | null
}
```

### MediaMetadata
```typescript
interface MediaMetadata {
  duration: number
  artist: string
  title: string
  album?: string
  resolution?: { width: number, height: number }
  fileSize: number
  hasSubtitles?: boolean
}
```

### PlaylistItem
```typescript
interface PlaylistItem {
  mediaFile: MediaFile
  addedAt: Date
  isDuplicate: boolean
  error: PlaylistItemError | null
}
```

### AppSettings
```typescript
interface AppSettings {
  mediaDirectory: string
  displayMode: 'single' | 'dual'
  volume: number
  audioBoostEnabled: boolean
  audioOutputDevice: string
  crossfadeEnabled: boolean
  crossfadeDuration: number
  autoPlayEnabled: boolean
  visualizationStyle: string
  theme: string
  fontSize: number
  keyboardShortcuts: Record<string, string>
  performanceSettings: {
    preloadBufferSize: number
    cacheSize: number
  }
}
```

## Error Handling

### Error Types

**File Errors:**
- Corrupted file
- Missing file
- Permission denied
- Unsupported format

**Playback Errors:**
- Codec not available
- Hardware acceleration failure
- Audio device unavailable
- Buffer underrun

**System Errors:**
- Media directory unavailable
- Disk space insufficient
- Cache corruption

### Error Recovery Strategies

**Graceful Degradation:**
- Continue playback from buffer when file becomes unavailable
- Skip problematic files and continue with next song
- Fall back to default audio device if selected device fails
- Use placeholder metadata/thumbnails until processing completes

**User Notification:**
- Visual error indicators in playlist
- Toast notifications for system errors
- Error logging for troubleshooting

**Auto-Recovery:**
- Retry failed operations once
- Clear error states on restart
- Validate playlist files on load

## Testing Strategy

### Unit Testing
- Metadata extraction logic
- Filename parsing patterns
- Playlist operations (add, remove, reorder)
- Search algorithm
- Settings validation

### Integration Testing
- Media playback with different formats
- Crossfade transitions
- Auto-refresh file monitoring
- Playlist save/load
- Multi-window coordination

### Performance Testing
- Large library handling (30,000 files)
- Search response time (< 300ms)
- UI responsiveness (< 100ms)
- Video playback smoothness
- Background processing impact

### End-to-End Testing
- Complete user workflows
- Single vs dual screen modes
- Playlist composer workflow
- Error recovery scenarios

## Implementation Considerations

### Audio Processing Architecture

The Web Audio API provides a node-based audio graph that's highly extensible for future audio features:

**Current Implementation:**
- Source Node → Gain Node (volume) → Destination (output device)
- Analyzer Node (branched) → Visualization Engine

**Future Extensions (supported by architecture):**
- **Audio Filtering**: BiquadFilter nodes for EQ, high-pass, low-pass filters
- **Audio Mixing**: Multiple source nodes with individual gain controls
- **Effects**: Convolver nodes (reverb), DynamicsCompressor, WaveShaper (distortion)
- **Pitch Shifting**: ScriptProcessor or AudioWorklet for advanced processing
- **Vocal Removal**: Stereo manipulation using ChannelSplitter/Merger nodes

The modular node architecture means these features can be added without refactoring the core playback system.

### Performance Optimization

**Lazy Loading:**
- Load metadata and thumbnails in background
- Virtualize long lists (search results, catalog)
- Cache frequently accessed data

**Efficient Search:**
- Use indexed search (Fuse.js or similar)
- Debounce search input
- Limit result set size

**Video Performance:**
- Hardware acceleration when available
- Preload next video for crossfade
- Optimize buffer sizes

### Cross-Platform Considerations

**Windows-Specific:**
- Default to Windows user media directory
- Use Windows-style file paths
- Integrate with Windows audio subsystem

**Future Extensibility:**
- Abstract file system operations
- Use platform-agnostic media APIs where possible
- Modular architecture for easy platform additions
- Web Audio API graph architecture supports adding audio processing nodes (filters, effects, mixers) without core changes

### Security Considerations

**File System Access:**
- Validate all file paths
- Handle permission errors gracefully
- Sanitize user input for file operations

**Configuration:**
- Validate settings before applying
- Prevent injection in playlist files
- Secure storage of user preferences

## UI Design

### Main Window Layout

**Single Screen Mode - Normal View:**
```
┌─────────────────────────────────────────────────────────┐
│ Menu Bar (File, View, Playback, Settings, Help)        │
├─────────────────────────────────────────────────────────┤
│ Toolbar (Media Dir, Search, Playlist Composer)         │
├──────────────────────┬──────────────────────────────────┤
│                      │                                  │
│  Search Results /    │   Video Output Area              │
│  Media Catalog       │   (or Audio Visualization)       │
│                      │                                  │
│  [Scrollable List]   │   [Maximize Button]              │
│  - Thumbnails        │                                  │
│  - Artist/Title      │                                  │
│  - Duration          ├──────────────────────────────────┤
│                      │   Current Playlist               │
│                      │   (Queue)                        │
│                      │                                  │
│                      │   [Reorderable List]             │
│                      │   - Now Playing indicator        │
│                      │   - Duplicate indicators         │
│                      │   - Error indicators             │
│                      │   - Remove buttons               │
│                      │                                  │
├──────────────────────┴──────────────────────────────────┤
│ Playback Controls (Play/Pause, Skip, Volume, Progress)  │
│ Status Bar (Current song, Time, Settings indicators)    │
└─────────────────────────────────────────────────────────┘
```

**Single Screen Mode - Maximized Video View:**
```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│                                                         │
│                                                         │
│              Video Playback (Fullscreen)                │
│              (or Audio Visualization)                   │
│                                                         │
│                                                         │
│  [▼] ← Collapsible handle (bottom edge)                │
├─────────────────────────────────────────────────────────┤
│ Subtitle Display Area (if enabled)                      │
└─────────────────────────────────────────────────────────┘

When handle is activated (clicked/hovered):
┌─────────────────────────────────────────────────────────┐
│              Video Playback (Fullscreen)                │
│              (or Audio Visualization)                   │
├─────────────────────────────────────────────────────────┤
│ Subtitle Display Area (if enabled)                      │
├─────────────────────────────────────────────────────────┤
│ [▲] [Search Bar] [Add to Playlist] [Minimize]          │
└─────────────────────────────────────────────────────────┘
```

**Maximized View Behavior:**
- Minimal UI by default - only small collapsible handle visible at bottom edge
- Handle expands on hover/click to reveal search and controls
- Auto-collapses after 3 seconds of inactivity
- Keyboard shortcuts remain active even when controls hidden

**Dual Screen Mode:**
Main window shows catalog and playlist (no video output), while playback window shows video on second screen.

**Key Features:**
- Single screen mode embeds video output in main window
- Maximize button expands video to fullscreen within main window
- Maximized view maintains search and add-to-playlist functionality via bottom toolbar
- Resizable split panes between catalog, video, and playlist
- Virtualized lists for performance with large libraries
- Drag-and-drop from catalog to playlist
- Context menus for quick actions
- Keyboard shortcuts for all major actions
- Toggle between single/dual screen modes via menu or keyboard shortcut

### Playback Window (Dual Screen Mode)

**Structure:**
```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│                                                         │
│              Video Playback Area                        │
│              (or Audio Visualization)                   │
│                                                         │
│                                                         │
├─────────────────────────────────────────────────────────┤
│ Subtitle Display Area (if enabled)                      │
└─────────────────────────────────────────────────────────┘
```

**Key Features:**
- Fullscreen support
- Minimal UI (no controls visible during playback)
- Subtitle positioning and styling
- Audio visualization for MP3 files
- Aspect ratio preservation

### Playlist Composer Window

**Structure:**
```
┌─────────────────────────────────────────────────────────┐
│ Title: "Build Playlist"                    [Save] [X]   │
├──────────────────────┬──────────────────────────────────┤
│                      │                                  │
│  Media Catalog       │   Playlist Being Built           │
│  (Browse/Search)     │                                  │
│                      │                                  │
│  [Search/Filter Box] │   [Playlist Name Input]          │
│  [Filter: Artist ▼]  │                                  │
│                      │                                  │
│  [Scrollable List]   │   [Reorderable List]             │
│  - Thumbnails        │   - Drag handles                 │
│  - Artist/Title      │   - Remove buttons               │
│  - Duration          │   - Duration totals              │
│  - Multi-select      │                                  │
│  - Add buttons       │                                  │
│                      │                                  │
├──────────────────────┴──────────────────────────────────┤
│ Actions: [Add Selected] [Clear All] [Shuffle]           │
│          [Load M3U] [Save M3U] [Save & Load for Play]   │
└─────────────────────────────────────────────────────────┘
```

**Key Features:**
- Independent window (can be on different screen)
- **Multi-select support**: Ctrl+Click and Shift+Click for selecting multiple songs
- **Filtering capabilities**: Filter by artist, title, and other metadata
- Drag-and-drop between catalog and composition panes
- Real-time search filtering in catalog
- Playlist duration calculation
- Save to M3U file (for later use)
- Save & Load for immediate playback (replaces current playlist)
- Load existing M3U for editing

### Component Specifications

**Media Item Card:**
- Thumbnail (120x90px for videos, album art for MP3)
- Artist name (primary text)
- Song title (secondary text)
- Duration badge
- Error indicator (if file has issues)
- Loading state (skeleton/spinner while metadata loads)

**Playlist Item:**
- Same as Media Item Card plus:
- Drag handle for reordering
- Remove button
- "Now Playing" indicator (animated)
- Duplicate warning badge
- Position number

**Search Bar:**
- Debounced input (300ms)
- Clear button
- Search history dropdown
- Loading indicator during search
- Result count display

**Playback Controls:**
- Play/Pause toggle button
- Previous/Next track buttons
- Volume slider with mute toggle
- Progress bar with time display (current/total)
- Shuffle and repeat toggles
- Audio device selector

**Settings Dialog:**
- Tabbed interface (General, Audio, Display, Keyboard)
- Real-time preview of changes
- Reset to defaults button
- Apply/Cancel buttons

### Interaction Patterns

**Adding Songs to Playlist:**
1. Double-click on media item in catalog
2. Drag-and-drop to playlist pane
3. Right-click → "Add to Playlist" (Next/End)
4. Select multiple + keyboard shortcut

**Reordering Playlist:**
1. Drag-and-drop items
2. Cut/paste with keyboard shortcuts
3. Right-click → Move Up/Down

**Search Workflow:**
1. Type in search box (auto-filters as you type)
2. Results update in real-time
3. Clear search to return to full catalog
4. Search history accessible via dropdown

**Error Handling UI:**
- Toast notifications for system errors (bottom-right)
- Inline error indicators in playlist items
- Error details on hover/click
- Retry action where applicable

### Visual States

**Media Items:**
- Default: Normal appearance
- Hover: Highlight background, show action buttons
- Selected: Distinct background color
- Loading: Skeleton placeholder or spinner
- Error: Red indicator, grayed out
- Playing: Animated indicator, highlighted

**Buttons:**
- Default: Normal appearance
- Hover: Slight highlight
- Active/Pressed: Darker shade
- Disabled: Grayed out, no interaction
- Focus: Visible outline for keyboard navigation

### Responsive Behavior

**Window Resizing:**
- Minimum window size: 1024x600px
- Split pane maintains proportions
- Lists reflow and virtualize
- Controls remain accessible

**Performance Considerations:**
- Virtualized lists (only render visible items)
- Lazy-load thumbnails (load as scrolled into view)
- Debounced search input
- Throttled scroll events

## UI/UX Design Principles

### Responsiveness
- All interactions < 100ms feedback
- Progress indicators for long operations
- Non-blocking background processing

### Consistency
- Unified keyboard shortcuts across modes
- Consistent error indication patterns
- Predictable window behavior

### Accessibility
- Full keyboard navigation
- Visual focus indicators
- Clear disabled state indication
- Screen reader support for key elements

### Default Keyboard Shortcuts

**Playback Controls:**
- `Space` - Play/Pause
- `Right Arrow` - Skip to next track
- `Left Arrow` - Previous track
- `Up Arrow` - Volume up
- `Down Arrow` - Volume down
- `M` - Mute/Unmute
- `F` - Toggle fullscreen (playback window)

**Playlist Management:**
- `Ctrl+A` - Add selected song to end of playlist
- `Ctrl+Shift+A` - Add selected song next in queue
- `Delete` - Remove selected song from playlist
- `Ctrl+L` - Clear playlist
- `Ctrl+S` - Shuffle playlist

**Navigation:**
- `Ctrl+F` - Focus search box
- `Ctrl+P` - Open Playlist Composer
- `Ctrl+,` - Open Settings
- `Ctrl+R` - Refresh media library
- `Ctrl+D` - Toggle single/dual screen mode
- `Escape` - Exit fullscreen or close dialog

**Selection:**
- `Ctrl+Click` - Multi-select items
- `Shift+Click` - Range select items
- `Ctrl+C` - Copy selected items
- `Ctrl+V` - Paste items to playlist

All shortcuts are customizable via Settings dialog.

### Professional Appearance
- Smooth transitions and animations
- Clean, uncluttered interfaces
- Appropriate information density
- Dark theme optimized for performance venues

## Deployment

### Build Process
- Electron builder for Windows executable
- Bundle all dependencies
- Include ffmpeg binaries for media processing

### Installation
- Single installer package
- Default settings configuration
- First-run setup (media directory default)

### Updates
- Version checking mechanism
- Auto-update capability (future)
- Settings migration between versions
