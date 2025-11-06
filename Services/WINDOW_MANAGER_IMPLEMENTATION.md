# Window Manager Implementation

## Overview

The Window Manager service manages window modes (single/dual screen), window states (position, size), fullscreen toggling, and cross-window communication using ReactiveUI MessageBus.

## Implementation Details

### Core Components

1. **IWindowManager Interface**
   - Defines the contract for window management operations
   - Supports mode switching, fullscreen toggling, and state persistence
   - Provides message broadcasting and subscription capabilities

2. **WindowManager Class**
   - Implements IWindowManager using ReactiveUI MessageBus
   - Manages window states and fullscreen states in memory
   - Persists window configurations to settings
   - Broadcasts messages for cross-window synchronization

### Key Features

#### Window Modes
- **Single Mode**: All functionality in one window (normal and video modes)
- **Dual Mode**: Separate playback and control windows
- Mode changes are persisted to settings
- Mode change events notify subscribers

#### Fullscreen Management
- Independent fullscreen state per window
- Toggle fullscreen for any window by ID
- Fullscreen state change events
- State tracking for multiple windows

#### Window State Persistence
- Saves window position, size, and maximized state
- Restores window states on application startup
- Per-window state tracking
- Automatic persistence to settings

#### Cross-Window Communication
- Uses ReactiveUI MessageBus for pub/sub messaging
- Type-safe message broadcasting
- Subscription management with IDisposable
- Supports custom message types

### Message Types

1. **WindowModeChangeMessage**: Broadcast when display mode changes
2. **OpenPlaylistComposerMessage**: Request to open Playlist Composer window
3. **ClosePlaylistComposerMessage**: Request to close Playlist Composer window
4. **ToggleFullscreenMessage**: Request to toggle fullscreen for a window
5. **RestoreWindowStateMessage**: Request to restore a window's saved state
6. **StateSyncMessage**: Generic state synchronization across windows

### Usage Examples

#### Switching Window Modes
```csharp
// Switch to dual screen mode
await windowManager.SetModeAsync(WindowMode.Dual);

// Switch to single screen mode
await windowManager.SetModeAsync(WindowMode.Single);

// Check current mode
var currentMode = windowManager.CurrentMode;
```

#### Managing Fullscreen
```csharp
// Toggle fullscreen for main window
windowManager.ToggleFullscreen("MainWindow");

// Check if window is fullscreen
bool isFullscreen = windowManager.IsFullscreen("MainWindow");
```

#### Window State Persistence
```csharp
// Update window state (called by window when moved/resized)
var state = new WindowState
{
    WindowId = "MainWindow",
    X = 100,
    Y = 100,
    Width = 1280,
    Height = 720,
    IsMaximized = false
};
windowManager.UpdateWindowState(state);

// Save all window states
await windowManager.SaveWindowStateAsync();

// Restore window states on startup
await windowManager.RestoreWindowStateAsync();
```

#### Message Broadcasting
```csharp
// Subscribe to mode change messages
var subscription = windowManager.Subscribe<WindowModeChangeMessage>(msg =>
{
    Console.WriteLine($"Mode changed from {msg.OldMode} to {msg.NewMode}");
});

// Broadcast custom message
windowManager.BroadcastMessage(new StateSyncMessage
{
    StateKey = "CurrentSong",
    StateValue = currentSong
});

// Dispose subscription when done
subscription.Dispose();
```

#### Opening Playlist Composer
```csharp
// Open Playlist Composer window
await windowManager.OpenPlaylistComposerAsync();

// Close Playlist Composer window
windowManager.ClosePlaylistComposer();
```

### Event Handling

```csharp
// Subscribe to mode change events
windowManager.ModeChanged += (sender, args) =>
{
    Console.WriteLine($"Mode changed: {args.OldMode} -> {args.NewMode}");
};

// Subscribe to fullscreen change events
windowManager.FullscreenChanged += (sender, args) =>
{
    Console.WriteLine($"Window {args.WindowId} fullscreen: {args.IsFullscreen}");
};
```

## Integration with Other Services

### Settings Manager
- Persists display mode preference
- Stores window states (position, size, maximized)
- Loads configuration on startup

### UI Layer (Future)
- Windows subscribe to messages for state updates
- Windows call UpdateWindowState when moved/resized
- Windows respond to fullscreen toggle messages
- Windows restore state on creation

## Testing

The implementation includes comprehensive tests:

1. **Mode Switching**: Tests single/dual mode transitions
2. **Fullscreen Toggle**: Tests fullscreen state management
3. **State Persistence**: Tests save/restore of window states
4. **Message Broadcasting**: Tests pub/sub messaging
5. **Playlist Composer**: Tests window open/close messages
6. **Event Handling**: Tests event raising for mode and fullscreen changes

Run tests with:
```bash
dotnet run --project TestWindowManager.cs
```

## Design Decisions

### ReactiveUI MessageBus
- Chosen for type-safe, reactive messaging
- Integrates well with Avalonia UI's reactive architecture
- Supports multiple subscribers per message type
- Clean subscription management with IDisposable

### State Management
- Window states stored in memory for fast access
- Persisted to settings for durability
- Separate tracking for fullscreen states
- Per-window state isolation

### Async/Await Pattern
- Async methods for I/O operations (settings persistence)
- Synchronous methods for in-memory operations
- Consistent with .NET async best practices

## Requirements Satisfied

- **Requirement 5**: Single screen mode support
- **Requirement 6**: Dual screen mode support
- **Requirement 12**: Fullscreen mode for any window
- Window state persistence (position, size)
- Cross-window communication via message bus
- State synchronization across windows

## Future Enhancements

1. **Window Validation**: Ensure restored window positions are on valid screens
2. **Multi-Monitor Support**: Detect and handle monitor configuration changes
3. **Window Animations**: Smooth transitions between modes
4. **State Versioning**: Handle settings migration for window state format changes
5. **Window Templates**: Save/load named window layout presets
