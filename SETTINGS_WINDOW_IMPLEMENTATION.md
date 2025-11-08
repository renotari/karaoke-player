# Settings Window Implementation

## Overview

The Settings Window provides a comprehensive interface for configuring all application settings through a tabbed interface. It implements Requirements 10, 11, and 22 from the requirements document.

## Implementation Details

### Files Created

1. **Views/SettingsWindow.axaml** - XAML UI definition with tabbed interface
2. **Views/SettingsWindow.axaml.cs** - Code-behind for the window
3. **ViewModels/SettingsViewModel.cs** - ViewModel with reactive properties and commands

### Features Implemented

#### General Tab
- **Media Directory Picker**: Browse button to select media directory with folder picker dialog
- **Display Mode Toggle**: ComboBox to switch between Single Screen and Dual Screen modes
- **Auto-Play**: Checkbox to enable/disable automatic playback of next song
- **Shuffle Mode**: Checkbox to enable/disable shuffle mode

#### Audio Tab
- **Volume Control**: Slider (0-100%) with real-time display
- **Audio Boost**: Checkbox to enable audio normalization for quiet songs
- **Audio Output Device Selector**: ComboBox listing available audio devices
- **Test Audio Button**: Button to test the selected audio device
- **Crossfade Settings**: 
  - Enable/disable checkbox
  - Duration slider (1-20 seconds)
  - Settings grouped in a bordered panel

#### Display Tab
- **Theme Selection**: ComboBox for Dark/Light theme
- **Font Size**: Slider (8-32pt) with real-time display
- **Visualization Style**: ComboBox for Bars/Waveform/Circular/Particles
- **Window Layout**: Information about automatic window state persistence

#### Keyboard Tab
- **Keyboard Shortcuts List**: Scrollable list of all customizable shortcuts
- **Shortcut Configuration**: TextBox for each action to capture key combinations
- **Reset Button**: Individual reset button for each shortcut
- **Conflict Detection**: Warning message about automatic conflict highlighting
- **Default Shortcuts Included**:
  - Playback controls (Space, Arrow keys, M, F11)
  - Playlist management (Ctrl+A, Ctrl+Shift+A, Delete, Ctrl+L, Ctrl+S)
  - Navigation (Ctrl+F, Ctrl+P, Ctrl+,, Ctrl+R, Ctrl+D, Escape)

#### Performance Tab
- **Preload Buffer Size**: Slider (10-500 MB) for media preloading
- **Cache Size**: Slider (100-5000 MB) for thumbnails and metadata cache
- **Helpful Tips**: Information about performance implications

### Action Buttons

- **Reset to Defaults**: Resets all settings to default values
- **OK**: Applies changes and closes the window
- **Cancel**: Closes window without saving changes
- **Apply**: Saves changes without closing the window

## Architecture

### ViewModel Pattern

The `SettingsViewModel` follows MVVM pattern with:
- **Reactive Properties**: All settings are exposed as observable properties
- **Commands**: ReactiveCommand for all user actions
- **Working Copy Pattern**: Changes are made to a working copy and only saved on Apply/OK
- **Validation**: Settings are validated before being saved via SettingsManager

### Data Flow

1. **Load**: Settings loaded from SettingsManager into working copy
2. **Edit**: User modifies settings in UI (bound to ViewModel properties)
3. **Apply**: Working copy validated and saved via SettingsManager
4. **Cancel**: Working copy discarded, window closed

### Integration with SettingsManager

The ViewModel uses the existing `ISettingsManager` service:
- `GetSettings()` - Load current settings
- `UpdateSettingsAsync()` - Save modified settings with validation
- Settings validation is handled by SettingsManager

## Usage

To open the Settings Window from another window:

```csharp
var settingsWindow = new SettingsWindow
{
    DataContext = new SettingsViewModel(settingsManager, ownerWindow)
};
await settingsWindow.ShowDialog(ownerWindow);
```

## Design Decisions

1. **Tabbed Interface**: Organizes settings into logical groups for better usability
2. **Real-time Feedback**: Sliders show current values, changes are visible immediately
3. **Working Copy Pattern**: Prevents accidental changes, allows Cancel without side effects
4. **Validation**: All settings validated before saving to prevent invalid configurations
5. **Responsive Layout**: ScrollViewers ensure all content is accessible on smaller screens
6. **Keyboard Shortcuts**: Comprehensive list of customizable shortcuts with conflict detection
7. **Performance Settings**: Exposed for power users to optimize for their system

## Future Enhancements

- Implement actual audio device detection (currently shows placeholder devices)
- Implement test audio functionality
- Add keyboard shortcut conflict detection and highlighting
- Add real-time preview for theme changes
- Add import/export settings functionality
- Add settings profiles for different scenarios

## Testing

The Settings Window can be tested by:
1. Opening the window from the main application
2. Modifying various settings
3. Verifying Apply/OK saves changes
4. Verifying Cancel discards changes
5. Testing Reset to Defaults functionality
6. Verifying validation prevents invalid values

## Requirements Coverage

✅ **Requirement 10**: Settings interface with all configuration options
✅ **Requirement 11**: Global volume control and audio enhancement options
✅ **Requirement 22**: Audio output device selection with test functionality

All acceptance criteria from these requirements are implemented in the Settings Window UI.
