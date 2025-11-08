# Manual Testing Guide - Current Implementation

## Overview

This guide covers what you can manually test with the current implementation (Tasks 1-19 complete). The application has all core services implemented and the main UI windows created.

## Prerequisites

### 1. Build and Run
```bash
dotnet build
dotnet run
```

### 2. Test Media Files
Create a test directory with some media files:
- MP4/MKV/WEBM video files
- MP3 audio files
- Mix of files with and without metadata

## What You Can Test Now

### ✅ 1. Application Startup

**Test**: Launch the application
```bash
dotnet run
```

**Expected**:
- Application launches without crashes
- MainWindow appears
- Default settings are loaded
- No error messages

**What to Check**:
- Window opens at reasonable size (1200x700)
- UI elements are visible and properly laid out
- Status bar shows "Ready" message

---

### ✅ 2. Settings Manager

**Test**: Settings persistence

**Steps**:
1. Run the application
2. Check if settings file is created at: `%APPDATA%/KaraokePlayer/settings.json` (Windows)
3. Close and reopen the application
4. Verify settings persist

**Expected**:
- Settings file created on first run
- Default values present
- Settings survive app restart

**Manual Verification Script**:
```bash
dotnet run --project . -- --test-settings
```

Or check the settings file directly:
```powershell Get-Content "$env:APPDATA\KaraokePlayer\settings.json"
```

---

### ✅ 3. Media Library Scanning

**Test**: Directory scanning and file indexing

**Steps**:
1. Create a test directory with media files
2. Configure the media directory in settings (or use default)
3. Launch the application
4. Observe the scan progress

**Expected**:
- Application scans the directory
- Files are indexed in the database
- File count appears in status bar
- Catalog shows media files

**What to Check**:
- All supported formats detected (MP4, MKV, WEBM, MP3)
- Subdirectories are scanned recursively
- Scan completes without errors
- File count is accurate

**Database Location**:
```
%APPDATA%/KaraokePlayer/karaoke.db
```

---

### ✅ 4. Metadata Extraction

**Test**: Metadata parsing from files

**Steps**:
1. Add media files with various metadata:
   - Files with ID3 tags (MP3)
   - Files with embedded metadata (MP4/MKV)
   - Files with no metadata (filename parsing)
2. Wait for background processing
3. Check catalog display

**Expected**:
- Artist and title extracted from metadata
- Filename parsing works for files without metadata
- Duration is calculated correctly
- Placeholder metadata shown while processing

**Filename Patterns to Test**:
- `Artist - Title.mp3`
- `Artist-Title.mp4`
- `Title (Artist).mkv`
- `Artist_Title.webm`

---

### ✅ 5. Thumbnail Generation

**Test**: Thumbnail creation for media files

**Steps**:
1. Add video and audio files
2. Wait for background thumbnail generation
3. Check catalog for thumbnails

**Expected**:
- Video thumbnails generated (frame at 10% duration)
- MP3 artwork extracted from ID3 tags
- Placeholder thumbnails for files without artwork
- Thumbnails cached for performance

**Cache Location**:
```
%APPDATA%/KaraokePlayer/cache/thumbnails/
```

---

### ✅ 6. Search Functionality

**Test**: Real-time search with filtering

**Steps**:
1. Type in the search box
2. Try searching by:
   - Artist name
   - Song title
   - Filename
   - Partial matches

**Expected**:
- Results update in real-time (300ms debounce)
- Partial matching works
- Search is case-insensitive
- "No results" message when nothing matches
- Search history is saved (last 10 searches)

**Performance Target**: < 300ms response time

---

### ✅ 7. Playlist Management

**Test**: Adding, removing, and reordering songs

**Steps**:
1. **Add songs**:
   - Double-click a song in catalog
   - Right-click → "Add to Playlist (Next)"
   - Right-click → "Add to Playlist (End)"
2. **Remove songs**:
   - Click the ✕ button on playlist items
3. **Reorder** (if drag-drop implemented):
   - Drag songs to reorder
4. **Clear playlist**:
   - Click "Clear" button
5. **Shuffle**:
   - Click "Shuffle" button

**Expected**:
- Songs added to correct position (next/end)
- Duplicate indicator shows for repeated songs
- Playlist persists when app closes
- Last playlist restored on startup
- UI updates immediately (< 50ms)

**Auto-Save Location**:
```
%APPDATA%/KaraokePlayer/current-playlist.json
```

---

### ✅ 8. Playlist Save/Load (M3U)

**Test**: M3U playlist export/import

**Steps**:
1. Create a playlist
2. Save as M3U file (File → Save Playlist)
3. Clear the playlist
4. Load the M3U file (File → Load Playlist)

**Expected**:
- M3U file created with correct format
- Playlist restored from M3U
- Missing files marked with error indicator
- Relative and absolute paths supported

---

### ✅ 9. Cache Manager

**Test**: Cache functionality and invalidation

**Steps**:
1. Let thumbnails and metadata cache build
2. Modify a media file (change metadata)
3. Observe cache invalidation
4. Check cache size limits

**Expected**:
- Cache stores thumbnails and metadata
- Modified files trigger cache invalidation
- LRU eviction when cache exceeds 500MB
- Cache persists between sessions

**Cache Stats** (if exposed in UI):
- Total cache size
- Hit rate
- Items per category

---

### ✅ 10. Main Window UI

**Test**: Single-screen mode interface

**Steps**:
1. Launch application
2. Test three-pane layout:
   - Left: Catalog with search
   - Center: Video output area
   - Right: Playlist
3. Resize panes using grid splitters
4. Test maximize video button

**Expected**:
- All panes visible and functional
- Grid splitters work smoothly
- Pane sizes persist between sessions
- Maximize button toggles video mode

---

### ✅ 11. Video Mode

**Test**: Maximized video view with collapsible controls

**Steps**:
1. Click maximize button (⛶) in video area
2. Observe video mode activation
3. Test collapsible control handle:
   - Click/hover to expand
   - Auto-collapse after 3 seconds
4. Use quick search in expanded handle
5. Exit video mode

**Expected**:
- Video area fills the window
- Control handle appears at bottom
- Handle expands/collapses smoothly
- Quick search works
- Exit button returns to normal mode

---

### ✅ 12. Playback Window

**Test**: Dual-screen mode playback window

**Steps**:
1. Switch to dual-screen mode (if implemented in UI)
2. Observe Playback Window creation
3. Test fullscreen (F11)
4. Test subtitle display area

**Expected**:
- Separate window for video playback
- Minimal UI (no controls visible)
- Fullscreen works
- Window position persists

**Note**: Dual-screen mode activation may need to be triggered programmatically if UI isn't complete.

---

### ✅ 13. Control Window

**Test**: Dual-screen mode control window

**Steps**:
1. Switch to dual-screen mode
2. Observe Control Window creation
3. Verify it shows placeholder UI
4. Test window positioning

**Expected**:
- Control Window opens
- Shows "Control Window - Dual Screen Mode" text
- Window is independent from Playback Window
- Position and size persist

**Current Status**: Minimal placeholder UI (can be expanded)

---

### ✅ 14. Window Manager

**Test**: Window coordination and state management

**Steps**:
1. Move and resize windows
2. Close and reopen application
3. Test fullscreen toggle (F11)
4. Switch between single/dual modes

**Expected**:
- Window positions saved
- Window sizes saved
- States restored on startup
- Fullscreen works in all windows
- Mode switching works

---

### ⚠️ 15. Media Playback (Limited Testing)

**Test**: Basic playback functionality

**Current Status**: Media Player Controller is implemented but may not be fully wired to UI

**Steps** (if playback controls are functional):
1. Add songs to playlist
2. Click Play button
3. Test pause, stop, skip
4. Adjust volume
5. Seek in progress bar

**Expected** (when fully wired):
- Video/audio plays
- Controls respond
- Volume adjusts
- Progress bar updates
- Auto-advance to next song

**Note**: LibVLC integration may require additional setup or testing

---

### ⚠️ 16. Crossfade (Limited Testing)

**Test**: Crossfade transitions

**Current Status**: Implemented but requires playback to be working

**Steps**:
1. Enable crossfade in settings
2. Set duration (1-20 seconds)
3. Play multiple songs
4. Observe transitions

**Expected**:
- Smooth audio fade out/in
- Video dip-to-black transition
- No stuttering or gaps
- Preloading works

---

### ⚠️ 17. Audio Visualization (Limited Testing)

**Test**: Visualizations for MP3 playback

**Current Status**: Engine implemented, needs playback integration

**Steps**:
1. Play an MP3 file
2. Observe visualization
3. Try different styles (if UI available)

**Expected**:
- Real-time visualization
- 30+ FPS performance
- Responds to audio spectrum
- Song info displayed

**Styles**: Bars, Waveform, Circular, Particles

---

## Testing Tools Included

### 1. Verification Scripts

Run the included verification scripts:

```bash
# Test Settings Manager
dotnet run --project . VerifySettingsManager

# Test Window Manager
dotnet run --project . VerifyWindowManager

# Test Playback Window
dotnet run --project . VerifyPlaybackWindow

# Test Audio Visualization
dotnet run --project . VerifyAudioVisualization
```

### 2. Unit Tests

Run unit tests for services:

```bash
dotnet test
```

Or run specific test files:
```bash
dotnet run --project . TestSearchEngine
dotnet run --project . TestPlaylistManager
dotnet run --project . TestWindowManager
```

---

## What You CANNOT Test Yet

### ❌ Not Implemented (Tasks 20-33)

1. **Playlist Composer Window** - Bulk playlist creation UI
2. **Settings Window** - Configuration UI
3. **Keyboard Shortcuts** - Global hotkeys
4. **Error Notifications** - Toast messages
5. **Logging System** - Error logging
6. **First-Run Experience** - Setup wizard
7. **Performance Optimizations** - Virtualization, lazy loading

---

## Performance Testing

### Targets to Verify

1. **Search Response**: < 300ms for any query
2. **UI Responsiveness**: < 100ms for interactions
3. **Playlist Operations**: < 50ms UI updates
4. **Startup Time**: < 3 seconds to ready state
5. **Library Size**: Support 30,000 files

### How to Test Performance

1. **Large Library Test**:
   - Create/copy 1,000+ media files
   - Measure scan time
   - Test search performance
   - Monitor memory usage

2. **UI Responsiveness**:
   - Add many songs to playlist
   - Test scroll performance
   - Measure button click response

3. **Memory Usage**:
   - Use Task Manager to monitor RAM
   - Target: < 300MB with 10,000 files loaded

---

## Common Issues to Watch For

### 1. Database Issues
- **Symptom**: Files not appearing in catalog
- **Check**: `%APPDATA%/KaraokePlayer/karaoke.db` exists
- **Fix**: Delete database and rescan

### 2. Cache Issues
- **Symptom**: Thumbnails not loading
- **Check**: Cache directory exists and has write permissions
- **Fix**: Clear cache directory

### 3. Settings Issues
- **Symptom**: Settings not persisting
- **Check**: Settings file location and permissions
- **Fix**: Delete settings file and restart

### 4. LibVLC Issues
- **Symptom**: Playback doesn't work
- **Check**: LibVLC binaries are in output directory
- **Fix**: Ensure NuGet packages restored correctly

---

## Reporting Issues

When you find issues, note:

1. **Steps to reproduce**
2. **Expected behavior**
3. **Actual behavior**
4. **Error messages** (if any)
5. **Log files** (when logging is implemented)
6. **System info** (OS, .NET version)

---

## Next Testing Phase

After implementing tasks 20-28, you'll be able to test:

- Playlist Composer workflow
- Settings configuration
- Keyboard shortcuts
- Error handling
- First-run experience
- Full integration scenarios

---

## Quick Test Checklist

Use this for quick smoke testing:

- [ ] Application launches
- [ ] Settings file created
- [ ] Media directory scanned
- [ ] Files appear in catalog
- [ ] Search works
- [ ] Can add songs to playlist
- [ ] Playlist persists
- [ ] Can save/load M3U
- [ ] Thumbnails generate
- [ ] Metadata extracted
- [ ] Window positions save
- [ ] Video mode works
- [ ] Build has no errors

---

## Summary

**Currently Testable**: Core services, UI layout, data management, window coordination
**Limited Testing**: Playback, crossfade, visualizations (need full integration)
**Not Yet Available**: Settings UI, Playlist Composer, keyboard shortcuts, error notifications

The foundation is solid and ready for the remaining UI windows and integration work!
