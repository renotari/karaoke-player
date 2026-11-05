# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Karaoke Player is a Windows karaoke and media player built with .NET 10 and Avalonia UI. It plays video (MP4, MKV, WEBM) and audio (MP3) files from a media folder, with a karaoke-style queue, crossfade, music-reactive visualizations for audio, single/dual-screen modes, and it is meant for large libraries (30,000+ files).

**Technology Stack:**
- Framework: .NET 10 (C#), self-contained publish for win-x64
- UI: Avalonia UI 11.3, MVVM with CommunityToolkit.Mvvm (ReactiveUI only for search throttling and MessageBus)
- Media: LibVLCSharp 3.9 / LibVLC 3.0
- Database: SQLite with Entity Framework Core 10
- Images: SkiaSharp 3 (thumbnails)
- Metadata: TagLib# (audio and video containers; falls back to file-name parsing)

## Build and Development Commands

The app targets Windows, but development happens on Linux (Ubuntu 26.04): the project builds and runs there too. On Linux the build targets the host platform and uses the system libvlc (`sudo apt install dotnet-sdk-10.0 vlc libvlc-dev`). The Windows-only bits (`win-x64` RID, self-contained, ReadyToRun, `VideoLAN.LibVLC.Windows`) are enabled when building on Windows or when passing `-r win-x64`.

```bash
dotnet build                                   # build (Linux: for local testing)
dotnet run                                     # run the app
dotnet run -- --test                           # service tests (TestRunner)
dotnet run -- --unit-tests                     # unit tests (RunUnitTests)
dotnet publish -c Release -r win-x64           # Windows build, also cross-compiled from Linux
build-release.bat / build-installer.bat        # Windows: release build / Inno Setup installer
```

Tests are plain runner classes (no xUnit): `Services/*Test.cs`, orchestrated by `TestRunner.cs`, `RunUnitTests.cs`, `IntegrationTests.cs`. Test modes set `KARAOKE_PLAYER_DATA_DIR` to a temporary folder (see `Program.cs`), so they never touch the real user data. The compiler must stay at 0 warnings.

### Running the GUI without a display (sandbox)

`xvfb`, `xdotool` and `imagemagick` are installed: run the app on `DISPLAY=:99` (`Xvfb :99 -screen 0 1600x900x24 &`), drive it with `xdotool`, screenshot with `import -window root shot.png`. There is no audio device there: start the app with `KARAOKE_PLAYER_VLC_OPTIONS="--aout=dummy"` or audio visualizations stay black.

## Architecture

### Startup and wiring (`App.axaml.cs`)

`App` creates every service by hand (`InitializeServices`) and the `MainWindowViewModel`. It also owns everything that is about windows rather than ViewModel state:
- `ApplySettings(AppSettings)`: applies theme, font size, volume, auto-play, shuffle mode, crossfade, audio normalization, visualization, audio device, keyboard shortcuts, display mode and media folder. Called at startup and on every `SettingsChanged` (the Settings dialog, the folder picker...). Changing the media folder triggers `ScanMediaDirectoryAsync` (incremental scan + monitoring + background metadata/thumbnail queues).
- Fullscreen and dual screen: reacts to `IsFullscreen` / `IsDualScreenMode` on the ViewModel, opens/closes `PlaybackWindow` (on a second monitor when present).
- Window layout (position, size, column widths) saved on exit, restored at startup.
- Global keyboard shortcuts (`KeyboardShortcutManager`), skipped while typing in a TextBox.

**Never block the UI thread on async code** (`.GetAwaiter().GetResult()`, `.Wait()`): it deadlocks (it once made the app hang on exit). Use `Task.Run(...)` if a sync wait is unavoidable.

### Threading rules

- LibVLC, `FileSystemWatcher` and timers raise events on background threads. `MainWindowViewModel.OnUiThread` marshals them to the UI thread; do the same for any new event handler that touches bound state.
- Throttled search uses `ViewModelBase.UiScheduler` (Avalonia's synchronization context), not `RxApp.MainThreadScheduler` (not bound to Avalonia without the ReactiveUI.Avalonia package).
- Never call LibVLC from a LibVLC event callback (move it to the thread pool).

### Playback (`Services/MediaPlayerController.cs`)

- Two slots, each with its own native video surface (`EmbeddedVideoView`, a `NativeControlHost` that exposes its handle). LibVLC cannot reuse a window another player is still using, so each slot renders into its own surface and the UI shows the one of `ActivePlayerIndex` (`IsVideoSurfaceA/BVisible`).
- Each slot has a player for video/plain audio and, created on demand, a player from a second `LibVLC` instance started with `--audio-visual=visual --effect-list=...`: audio visualizations are an instance-level option (per-media options are ignored, and on the main instance they would add a visualization window to videos).
- Crossfade: the ViewModel keeps the next song preloaded (`PreloadNextAsync` / `ClearPreload`); near the end the controller starts the other slot, makes it the current one immediately (`CrossfadeStarted` event, no `MediaEnded` for the outgoing song) and fades the two volumes. Events from non-current players are ignored.
- LibVLCSharp raises events with its internal event manager as `sender`: handlers are bound per player with closures (`Subscribe`).
- `KARAOKE_PLAYER_VLC_OPTIONS` adds LibVLC command-line options (troubleshooting).
- Nothing can be drawn by Avalonia on top of a native video surface: overlays go next to it (e.g. the title strip under audio visualizations, the Video Mode panel below the video).

### Playlist model (`MainWindowViewModel` + `PlaylistManager`)

Karaoke queue: `CurrentPlaylist[0]` is the song playing (while `CurrentSong != null`), the rest is the queue. A finished/skipped song leaves the list and goes to a history used by Previous. "Next" inserts after the song playing (on top when idle). `PlaylistManager` is the single source of truth: commands call it, and `CurrentPlaylist` is rebuilt from its `PlaylistChanged` event. The playlist is auto-saved to `current-playlist.json`.

### Library

`MediaLibraryManager` scans/monitors the folder and raises `FilesAdded/Removed/Modified`; `MetadataExtractor` and `ThumbnailGenerator` process queues in the background and raise events; the ViewModel updates every instance of the file (`MediaFile` implements `INotifyPropertyChanged` via `NotifyChanged()`). Thumbnails are decoded off-screen with LibVLC video callbacks and saved with SkiaSharp; `ThumbnailConverter` turns the path into a bitmap. `MediaFile.DisplayArtist/DisplayTitle/DisplayName` fall back to `FilenameParser` when metadata is missing.

### Views

- `MainWindow`: catalog, video area, playlist; Video Mode hides the side columns; in dual screen the center shows a panel instead of the video.
- `PlaybackWindow`: dual-screen audience window, bound to the same `MainWindowViewModel`.
- `SettingsWindow` (`SettingsViewModel` gets the dialog as owner), `PlaylistComposerWindow` (its ViewModel needs `SetWindow`), `WelcomeDialog` (first run).
- Time values are seconds (`double`): format them with `SecondsToTimeConverter`, not `StringFormat`.

### Data locations (`Services/AppPaths.cs`)

`%APPDATA%\KaraokePlayer\` on Windows, `~/.config/KaraokePlayer/` on Linux (override: `KARAOKE_PLAYER_DATA_DIR`): `settings.json`, `karaoke.db`, `current-playlist.json`, `Logs/`, `Thumbnails/`, `Playlists/`. `AppSettings` is also an EF entity: new settings that live only in the JSON must be `[NotMapped]` (otherwise a migration is needed), and must be copied in `SettingsViewModel.CloneSettings`.

### Not wired yet

- `CacheManager` (created, not used) and the Performance settings (`PreloadBufferSize`, `CacheSize`): no effect yet.
- `ErrorHandlingService` is only used to clear error states at startup.
- The Playlist Composer drag-and-drop uses Avalonia's legacy `DataObject` API (warnings suppressed in that file).

## Installer and Deployment

`Setup.iss` (Inno Setup) packages `bin\Release\net10.0\win-x64\publish\` (self-contained: no .NET runtime needed on the target PC). When bumping the version update `KaraokePlayer.csproj`, `Setup.iss` and `README.md`. See `docs/DEPLOYMENT.md`.
