# Playlist Composer Keyboard Shortcut Fix

## Issue
Pressing Ctrl+P did not open the Playlist Composer window. The `OpenPlaylistComposer()` method in MainWindowViewModel was just a stub with a TODO comment.

## Solution

### 1. Added Window Opening Methods to MainWindow.axaml.cs
- `OpenPlaylistComposerAsync()` - Creates and shows PlaylistComposerWindow as a dialog
- `OpenSettingsAsync()` - Creates and shows SettingsWindow as a dialog

### 2. Implemented Message-Based Communication
- Used ReactiveUI MessageBus for decoupling ViewModel from View
- MainWindowViewModel sends `OpenPlaylistComposerMessage` when Ctrl+P is pressed
- MainWindow subscribes to these messages and opens the appropriate window

### 3. Added OpenSettingsMessage Class
- Added `OpenSettingsMessage` class to WindowManager.cs alongside existing message classes
- This enables Ctrl+, (Ctrl+Comma) to open settings as well

### 4. Updated MainWindowViewModel
- `OpenPlaylistComposer()` now sends message via MessageBus
- `OpenSettings()` now sends message via MessageBus

## Files Modified
- `Views/MainWindow.axaml.cs` - Added window opening methods and message subscriptions
- `ViewModels/MainWindowViewModel.cs` - Implemented OpenPlaylistComposer and OpenSettings methods
- `Services/WindowManager.cs` - Added OpenSettingsMessage class

## Testing
1. Run the application
2. Press **Ctrl+P** - Playlist Composer window should open
3. Press **Ctrl+,** (Ctrl+Comma) - Settings window should open

## Notes
- Windows open as modal dialogs (blocking the main window until closed)
- Proper cleanup of message subscriptions when MainWindow closes
- This fix also enables the Settings keyboard shortcut which was previously not working
