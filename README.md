# Karaoke Player

A professional karaoke and media player application for Windows with advanced features including crossfade transitions, audio visualizations, and flexible single/dual-screen configurations.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)

## Features

### Core Functionality
- **Multi-Format Support**: Play MP4, MKV, WEBM video files and MP3 audio files
- **Smart Media Library**: Automatic directory scanning with metadata extraction and thumbnail generation
- **Advanced Search**: Fast search across 30,000+ files with partial matching and search history
- **Karaoke Queue**: the song playing is on top, finished songs leave the queue; add songs "next" or at the end, reorder, shuffle, play now; save/load playlists (M3U/M3U8)
- **Crossfade**: audio crossfade between songs with configurable duration (1-20 seconds); the video switches to the next song when the fade starts

### Display Modes
- **Single Screen Mode**: All-in-one interface with video player, catalog, and playlist
- **Video Mode**: Maximized video display with collapsible control handle for quick access
- **Dual Screen Mode**: fullscreen playback window on the second monitor, catalog and playlist on the main one
- **Fullscreen Support**: Immersive playback experience with F11 toggle

### Audio Features
- **Audio Visualizations**: music-reactive visualizations for MP3 files (spectrum bars, oscilloscope, spectrometer, VU meter) or a simple title screen
- **Volume Control**: volume remembered between sessions, optional normalization for quiet/loud songs
- **Audio Device Selection**: Route sound to specific output devices (PA systems, external speakers)
- **Subtitle Support**: Toggle embedded subtitles on/off for video files

### Professional Tools
- **Playlist Composer**: Dedicated window for building playlists with multi-select and drag-and-drop
- **Auto-Refresh**: Automatically detect new files added to media directory
- **Error Handling**: Graceful recovery from corrupted files, missing files, and playback errors
- **Keyboard Shortcuts**: customizable in Settings (they do not interfere with typing in the search box)

## System Requirements

### Minimum Requirements
- **OS**: Windows 10 (64-bit) or later
- **Processor**: Intel Core i3 or equivalent
- **Memory**: 4 GB RAM
- **Storage**: 500 MB free space (plus space for media files)
- **Graphics**: DirectX 11 compatible graphics card

### Recommended Requirements
- **OS**: Windows 11 (64-bit)
- **Processor**: Intel Core i5 or equivalent
- **Memory**: 8 GB RAM
- **Storage**: 1 GB free space (SSD recommended)
- **Graphics**: DirectX 12 compatible graphics card with hardware acceleration

### Dependencies
- **.NET runtime**: included (self-contained build), nothing to install
- **LibVLC**: Bundled with the application

## Installation

### Using the Installer (Recommended)

1. Download `KaraokePlayer-Setup-1.0.0.exe` from the releases page
2. Run the installer and follow the setup wizard
3. The installer will:
   - Install the application to `C:\Program Files\Karaoke Player`
   - Create desktop and start menu shortcuts
   - Set up application data directory

### Manual Installation

1. Extract the application files to your desired location
2. Run `KaraokePlayer.exe`

## Documentation

For detailed documentation, see the [docs/](docs/) directory:

- **[Quick Start Guide](docs/QUICKSTART.md)** - Get started quickly
- **[Manual Testing Guide](docs/MANUAL_TESTING_GUIDE.md)** - Testing procedures
- **[Deployment Guide](docs/DEPLOYMENT.md)** - Deployment instructions
- **[Unit Tests](docs/UNIT_TESTS_TASK31_SUMMARY.md)** - Unit test documentation
- **[Integration Tests](docs/INTEGRATION_TESTS_SUMMARY.md)** - Integration test documentation

## Getting Started

### First Launch

1. **Welcome Dialog**: On first launch, you'll be prompted to select your media directory
   - Default: Windows user media directory (`C:\Users\[YourName]\Music`)
   - Type the path or browse to your karaoke/music folder

2. **Initial Scan**: The application will scan your media directory
   - Quick scan indexes all supported files (MP4, MKV, WEBM, MP3)
   - Metadata and thumbnails are processed in the background
   - You can start using the app immediately while processing continues

### Basic Usage

#### Playing Songs

1. **Search**: Type in the search box to find songs by artist, title, or filename
2. **Add to Playlist**: Double-click a song (it plays next) or right-click for "next"/"end"
3. **Playback**: Click Play or press Space to start playback
4. **Queue Management**: 
   - Add songs "next" (default) or at "end" of playlist
   - Right-click a playlist song: play now, play next, move up/down, remove
   - Double-click a playlist song to play it right away

#### Display Modes

**Single Screen Mode** (Default):
- Video player, catalog, and playlist in one window
- Toggle to Video Mode for maximized video display
- Collapsible control handle for quick song requests

**Dual Screen Mode**:
- Playback Window: full-screen video on the second monitor (shows "Up next" between songs)
- Main window: catalog and playlist
- Switch modes via `Ctrl+D`, the View menu or Settings

#### Keyboard Shortcuts

**Playback**:
- `Space` - Play/Pause
- `Right Arrow` - Next track
- `Left Arrow` - Previous track
- `Up/Down Arrow` - Volume control
- `M` - Mute/Unmute
- `F11` - Fullscreen

**Playlist**:
- `Ctrl+A` - Add to end
- `Ctrl+Shift+A` - Add next
- `Delete` - Remove from playlist
- `Ctrl+L` - Clear playlist
- `Ctrl+S` - Shuffle

**Navigation**:
- `Ctrl+F` - Focus search
- `Ctrl+P` - Open Playlist Composer
- `Ctrl+,` - Open Settings
- `Ctrl+R` - Refresh library
- `Ctrl+D` - Toggle single/dual screen
- `Escape` - Exit fullscreen, then Video Mode

Shortcuts do not fire while typing in a text box (except function keys and Escape).

### Advanced Features

#### Crossfade Transitions

1. Open Settings (`Ctrl+,`)
2. Navigate to Audio tab
3. Enable crossfade and set duration (1-20 seconds)
4. Near the end of each song the next one starts: the two audio tracks fade into each other and the video switches to the new song

#### Playlist Composer

1. Open Playlist Composer (`Ctrl+P`)
2. Browse/search your entire catalog on the left
3. Multi-select songs (Ctrl+Click, Shift+Click)
4. Add to composition pane on the right
5. Reorder, remove, or shuffle as needed
6. Save as M3U file or load directly for playback

#### Audio Visualizations

For MP3 files, the screen shows a music-reactive visualization (drawn by VLC) with title and artist underneath:
- **Spectrum bars**
- **Oscilloscope**
- **Spectrometer**
- **VU meter**
- **None**: title and artist only

Change style in Settings > Display > Audio Visualization Style (applies from the next song)

## Configuration

### Settings Overview

**General Tab**:
- Media directory location
- Display mode (Single/Dual screen)
- Auto-play and shuffle options

**Audio Tab**:
- Volume and audio boost (volume normalization)
- Audio output device selection, with a test tone button
- Crossfade settings (enable, duration)

**Display Tab**:
- Theme selection
- Font size
- Visualization style
- Window positions, sizes and column widths are remembered automatically

**Keyboard Tab**:
- Customize all keyboard shortcuts
- Reset to defaults

### File Locations

- **Application Data**: `%APPDATA%\KaraokePlayer`
- **Settings**: `%APPDATA%\KaraokePlayer\settings.json`
- **Database**: `%APPDATA%\KaraokePlayer\karaoke.db`
- **Thumbnails**: `%APPDATA%\KaraokePlayer\Thumbnails`
- **Logs**: `%APPDATA%\KaraokePlayer\Logs`
- **Current Playlist**: `%APPDATA%\KaraokePlayer\current-playlist.json`

## Troubleshooting

### Common Issues

**Application won't start**:
- Check Windows Event Viewer for error details
- Review log files in `%APPDATA%\KaraokePlayer\Logs`

**Video playback issues**:
- Ensure graphics drivers are up to date
- Try disabling hardware acceleration in Settings
- Check if the video file plays in VLC Media Player

**Audio device not detected**:
- Restart the application after connecting audio devices
- Check Windows Sound settings
- Use the "Test Audio" button in Settings

**Slow performance with large libraries**:
- Wait for initial metadata/thumbnail processing to complete
- Reduce cache size in Settings > Performance
- Close other resource-intensive applications

**Files not appearing in catalog**:
- Verify files are in supported formats (MP4, MKV, WEBM, MP3)
- Check file permissions
- Use Refresh Library (`Ctrl+R`)

### Error Indicators

- **Red indicator**: File is corrupted or cannot be played
- **Yellow indicator**: File is missing or moved
- **Orange indicator**: Permission issues
- **Blue indicator**: Song is already in playlist (duplicate)

### Getting Help

- Check log files for detailed error information
- Review the troubleshooting section above
- Submit issues on GitHub: [Your Repository URL]

## Building from Source

### Prerequisites

- .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
- Visual Studio 2022 or JetBrains Rider (optional)
- Git

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/karaoke-player.git
   cd karaoke-player
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the application:
   ```bash
   dotnet build --configuration Release
   ```

4. Run the application:
   ```bash
   dotnet run
   ```

### Creating a Release Build

1. Publish for Windows x64:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained
   ```

2. Output will be in: `bin\Release\net10.0\win-x64\publish\`

### Creating an Installer

1. Install Inno Setup: https://jrsoftware.org/isinfo.php

2. Build the release version:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained
   ```

3. Compile the installer:
   - Open `Setup.iss` in Inno Setup
   - Click Build > Compile
   - Installer will be created in `installer\` directory

## Technology Stack

- **Framework**: .NET 10 (C#)
- **UI Framework**: Avalonia UI 11.3
- **Media Engine**: LibVLCSharp 3.9
- **Database**: SQLite with Entity Framework Core 9.0
- **State Management**: ReactiveUI 22.2
- **Metadata**: TagLib# 2.3
- **Graphics**: SkiaSharp 3.119

## Performance

The application is optimized for:
- **Library Size**: Up to 30,000 media files
- **Search Speed**: < 300ms response time
- **UI Responsiveness**: < 100ms for all interactions
- **Startup Time**: < 2 seconds
- **Memory Usage**: < 300MB with 10,000 files loaded
- **Video Playback**: Smooth 30+ FPS with hardware acceleration

## License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## Acknowledgments

- **LibVLC**: VLC media player library for robust media playback
- **Avalonia UI**: Cross-platform UI framework
- **TagLib#**: Metadata extraction library
- **SkiaSharp**: 2D graphics library

## Version History

### Version 1.0.0 (Initial Release)
- Complete karaoke player functionality
- Single and dual screen modes
- Crossfade transitions
- Audio visualizations
- Playlist composer
- Comprehensive keyboard shortcuts
- Error handling and logging
- Performance optimizations for large libraries

---

**Enjoy your karaoke experience!** 🎤🎵
