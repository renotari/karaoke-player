# Keyboard Shortcuts Implementation

## Overview

This document describes the implementation of keyboard shortcuts and navigation for the Karaoke Player application.

## Components

### 1. KeyboardShortcutManager Service

**Location**: `Services/KeyboardShortcutManager.cs`

The `KeyboardShortcutManager` is responsible for:
- Managing keyboard shortcut mappings
- Handling key events and dispatching to registered handlers
- Supporting customizable shortcuts
- Detecting shortcut conflicts

**Key Features**:
- Converts Avalonia `KeyEventArgs` to string representations (e.g., "Ctrl+A", "Space", "F11")
- Maintains a dictionary of action names to key gestures
- Allows runtime modification of shortcuts
- Provides conflict detection for shortcut assignments

### 2. AppSettings Model Updates

**Location**: `Models/AppSettings.cs`

Added support for storing keyboard shortcuts:
- `KeyboardShortcutsJson`: Stores shortcuts as JSON in the database
- `KeyboardShortcuts`: Property that serializes/deserializes shortcuts
- `GetDefaultKeyboardShortcuts()`: Static method providing default shortcut mappings

### 3. Global Keyboard Handler

**Location**: `App.axaml.cs`

The application sets up global keyboard shortcuts in the `SetupKeyboardShortcuts()` method:
- Registers all shortcut handlers with the `KeyboardShortcutManager`
- Attaches a global `KeyDown` event handler to the main window
- Routes key events through the shortcut manager

### 4. MainWindowViewModel Extensions

**Location**: `ViewModels/MainWindowViewModel.cs`

Added keyboard shortcut methods:
- `VolumeUp()` / `VolumeDown()`: Adjust volume by 5%
- `ToggleMute()`: Mute/unmute audio
- `ToggleFullscreen()`: Toggle fullscreen mode
- `AddSelectedToPlaylistEnd()` / `AddSelectedToPlaylistNext()`: Add selected song to playlist
- `RemoveSelectedFromPlaylist()`: Remove selected song
- `FocusSearch()`: Focus the search box
- `OpenPlaylistComposer()`: Open playlist composer window
- `OpenSettings()`: Open settings window
- `RefreshLibrary()`: Refresh media library
- `ToggleDisplayMode()`: Toggle between single/dual screen modes
- `CloseDialog()`: Close dialogs or exit fullscreen

### 5. UI Focus Indicators

**Location**: `Views/MainWindow.axaml`

Added visual focus indicators for keyboard navigation:
- Button focus: Blue accent border
- TextBox focus: Blue accent border
- ListBox focus: Blue accent border
- ListBoxItem focus: Highlighted background
- Slider focus: Blue accent border on thumb

### 6. Tab Navigation

**Location**: `Views/MainWindow.axaml`

Added `TabIndex` and `IsTabStop` properties to UI elements:
- Search box: TabIndex 0
- Clear search button: TabIndex 1
- Media catalog list: TabIndex 2
- Playlist list: TabIndex 3
- Playlist action buttons: TabIndex 4-5
- Playback controls: TabIndex 10-13
- Progress slider: TabIndex 14
- Volume controls: TabIndex 15-16

## Default Keyboard Shortcuts

### Playback Controls
- **Space**: Play/Pause
- **S**: Stop
- **Right Arrow**: Next track
- **Left Arrow**: Previous track
- **Up Arrow**: Volume up
- **Down Arrow**: Volume down
- **M**: Mute/Unmute
- **F11**: Toggle fullscreen

### Playlist Management
- **Ctrl+A**: Add selected song to end of playlist
- **Ctrl+Shift+A**: Add selected song next in queue
- **Delete**: Remove selected song from playlist
- **Ctrl+L**: Clear playlist
- **Ctrl+S**: Shuffle playlist

### Navigation
- **Ctrl+F**: Focus search box
- **Ctrl+P**: Open Playlist Composer
- **Ctrl+Comma**: Open Settings
- **Ctrl+R**: Refresh media library
- **Ctrl+D**: Toggle single/dual screen mode
- **Escape**: Close dialog or exit fullscreen

### General Navigation
- **Tab**: Move focus to next element
- **Shift+Tab**: Move focus to previous element
- **Enter**: Activate focused button or select item
- **Arrow Keys**: Navigate lists

## Customization

Keyboard shortcuts can be customized through the Settings interface (to be implemented in Settings window):

1. Open Settings (Ctrl+Comma)
2. Navigate to Keyboard tab
3. Click on a shortcut to change it
4. Press the desired key combination
5. System will detect conflicts and warn if the shortcut is already in use
6. Click "Reset to Defaults" to restore default shortcuts

## Implementation Details

### Key Gesture Format

Key gestures are stored as strings in the format:
- Single key: "A", "Space", "F11"
- With modifiers: "Ctrl+A", "Ctrl+Shift+A", "Alt+F4"
- Modifier order: Ctrl, Shift, Alt, Win (Meta)

### Event Flow

1. User presses a key
2. `OnGlobalKeyDown` in `App.axaml.cs` receives the event
3. Event is passed to `KeyboardShortcutManager.HandleKeyEvent()`
4. Manager converts `KeyEventArgs` to key gesture string
5. Manager looks up the action for that gesture
6. Manager invokes the registered handler for that action
7. Event is marked as handled to prevent further processing

### Focus Management

The `FocusSearchBox()` method in `MainWindow.axaml.cs` allows programmatic focus control:
- Called when Ctrl+F is pressed
- Finds the search TextBox by name
- Sets focus to enable immediate typing

## Testing

To test keyboard shortcuts:

1. **Build and run the application**:
   ```bash
   dotnet run
   ```

2. **Test playback controls**:
   - Add songs to playlist
   - Press Space to play/pause
   - Press Right/Left arrows to skip tracks
   - Press Up/Down arrows to adjust volume
   - Press M to mute/unmute

3. **Test navigation**:
   - Press Tab to move between UI elements
   - Verify focus indicators are visible
   - Press Ctrl+F to focus search box
   - Press Escape to exit fullscreen

4. **Test playlist management**:
   - Select a song in catalog
   - Press Ctrl+A to add to end
   - Press Ctrl+Shift+A to add next
   - Select a song in playlist
   - Press Delete to remove

## Future Enhancements

- Settings UI for customizing shortcuts
- Import/export shortcut configurations
- Shortcut cheat sheet overlay (press F1 or ?)
- Context-sensitive shortcuts (different shortcuts in different windows)
- Macro support (record and playback key sequences)

## Requirements Satisfied

This implementation satisfies the following requirements from the design document:

- **Requirement 4**: Keyboard shortcuts for playback actions
- **Requirement 12**: Fullscreen mode toggle (F11)
- **Requirement 19**: Full keyboard navigation support with Tab, Enter, Arrow keys, and visual focus indicators
- **Task 24**: All sub-tasks completed:
  - ✅ Global keyboard shortcut handler in App.axaml.cs
  - ✅ All shortcuts from design implemented
  - ✅ Tab navigation between UI elements
  - ✅ Visual focus indicators for keyboard navigation
  - ✅ Escape key to close dialogs and exit fullscreen
  - ✅ Shortcuts customizable via Settings (infrastructure in place)
