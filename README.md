# Karaoke Player

A professional karaoke and media player application for Windows with advanced features including crossfade transitions, audio visualizations, and flexible single/dual-screen configurations.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)

## Features

### Core Functionality
- **Multi-Format Support**: Play MP4, MKV, WEBM video files and MP3 audio files
- **Smart Media Library**: Automatic directory scanning with metadata extraction and thumbnail generation
- **Advanced Search**: Fast search across 30,000+ files with partial matching and search history
- **Playlist Management**: Queue songs, reorder, shuffle, and save/load playlists (M3U/M3U8 format)
- **Seamless Crossfade**: Professional-grade transitions between songs with configurable duration (1-20 seconds)

### Display Modes
- **Single Screen Mode**: All-in-one interface with video player, catalog, and playlist
- **Video Mode**: Maximized video display with collapsible control handle for quick access
- **Dual Screen Mode**: Separate playback and control windows for multi-monitor setups
- **Fullscreen Support**: Immersive playback experience with F11 toggle

### Audio Features
- **Audio Visualizations**: 4 styles (bars, waveform, circular, particles) for MP3 playback
- **Global Volume Control**: Consistent audio levels with boost mode for quiet songs
- **Audio Device Selection**: Route sound to specific output devices (PA systems, external speakers)
- **Subtitle Support**: Toggle embedded subtitles on/off for video files

### Professional Tools
- **Playlist Composer**: Dedicated window for building playlists with multi-select and drag-and-drop
- **Auto-Refresh**: Automatically detect new files added to media directory
- **Error Handling**: Graceful recovery from corrupted files, missing files, and playback errors
- **Keyboard Shortcuts**: Fully customizable shortcuts for efficient operation

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
- **.NET 8 Runtime**: Automatically checked during installation
- **LibVLC**: Bundled with the application

## Installation

### Using the Installer (Recommended)

1. Download `KaraokePlayer-Setup-1.0.0.exe` from the releases page
2. Run the installer and follow the setup wizard
3. The installer will:
   - Check for .NET 8 Runtime (prompt to download if missing)
   - Install the application to `C:\Program Files\Karaoke Player`
   - Create desktop and start menu shortcuts
   - Set up application data directory

### Manual Installation

1. Ensure .NET 8 Runtime is installed: https://dotnet.microsoft.com/download/dotnet/8.0
2. Extract the application files to your desired location
3. Run `KaraokePlayer.exe`

## Getting Started

### First Launch

1. **Welcome Dialog**: On first launch, you'll be prompted to select your media directory
   - Default: Windows user media directory (`C:\Users\[YourName]\Music`)
   - Or browse to your custom karaoke/music folder

2. **Initial Scan**: The application will scan your media directory
   - Quick scan indexes all supported files (MP4, MKV, WEBM, MP3)
   - Metadata and thumbnails are processed in the background
   - You can start using the app immediately while processing continues

### Basic Usage

#### Playing Songs

1. **Search**: Type in the search box to find songs by artist, title, or filename
2. **Add to Playlist**: Double-click a song or drag it to the playlist pane
3. **Playback**: Click Play or press Space to start playback
4. **Queue Management**: 
   - Add songs "next" (default) or at "end" of playlist
   - Drag to reorder songs in the playlist
   - Right-click for additional options

#### Display Modes

**Single Screen Mode** (Default):
- Video player, catalog, and playlist in one window
- Toggle to Video Mode for maximized video display
- Collapsible control handle for quick song requests

**Dual Screen Mode**:
- Playback Window: Full-screen video on second monitor
- Control Window: Catalog and playlist on primary monitor
- Switch modes via `Ctrl+D` or Settings

#### Keyboard Shortcuts

**Playback**:
- `Space` - Play/Pause
- `Right Arrow` - Next track
- `Left Arrow` - Previous track
- `Up/Down Arrow` - Volume control
- `M` - Mute/Unmute
- `F` or `F11` - Fullscreen

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
- `Escape` - Exit fullscreen/close dialog

### Advanced Features

#### Crossfade Transitions

1. Open Settings (`Ctrl+,`)
2. Navigate to Audio tab
3. Enable crossfade and set duration (1-20 seconds)
4. Songs will seamlessly transition with audio/video fade effects

#### Playlist Composer

1. Open Playlist Composer (`Ctrl+P`)
2. Browse/search your entire catalog on the left
3. Multi-select songs (Ctrl+Click, Shift+Click)
4. Add to composition pane on the right
5. Reorder, remove, or shuffle as needed
6. Save as M3U file or load directly for playback

#### Audio Visualizations

For MP3 files, choose from 4 visualization styles:
- **Bars**: Classic frequency bars
- **Waveform**: Oscilloscope-style display
- **Circular**: Radial frequency visualization
- **Particles**: Dynamic particle system

Change style in Settings > Display > Visualization Style

## Configuration

### Settings Overview

**General Tab**:
- Media directory location
- Display mode (Single/Dual screen)
- Auto-play and shuffle options

**Audio Tab**:
- Volume and audio boost
- Audio output device selection
- Crossfade settings (enable, duration)
- Test audio button

**Display Tab**:
- Theme selection
- Font size
- Visualization style
- Window layout preferences

**Keyboard Tab**:
- Customize all keyboard shortcuts
- Conflict detection
- Reset to defaults

### File Locations

- **Application Data**: `%APPDATA%\KaraokePlayer`
- **Settings**: `%APPDATA%\KaraokePlayer\settings.json`
- **Database**: `%APPDATA%\KaraokePlayer\karaoke.db`
- **Cache**: `%APPDATA%\KaraokePlayer\cache`
- **Logs**: `%APPDATA%\KaraokePlayer\logs`
- **Current Playlist**: `%APPDATA%\KaraokePlayer\current-playlist.json`

## Troubleshooting

### Common Issues

**Application won't start**:
- Verify .NET 8 Runtime is installed
- Check Windows Event Viewer for error details
- Review log files in `%APPDATA%\KaraokePlayer\logs`

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

- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
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

2. Output will be in: `bin\Release\net8.0\win-x64\publish\`

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

- **Framework**: .NET 8 (C#)
- **UI Framework**: Avalonia UI 11.3
- **Media Engine**: LibVLCSharp 3.9
- **Database**: SQLite with Entity Framework Core 9.0
- **State Management**: ReactiveUI 22.2
- **Metadata**: TagLib# 2.3, MediaInfo.Wrapper 21.9
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
