# Manual Testing Guide - PlaybackWindow

## Current Testing Status

At this stage, the **PlaybackWindow UI and ViewModel** are fully implemented, but they're not yet integrated into the main application flow. Here's what you can test now and what's coming next.

## What's Currently Implemented

✅ **PlaybackWindow.axaml** - Complete UI layout
✅ **PlaybackWindow.axaml.cs** - Event handling and visualization rendering
✅ **PlaybackWindowViewModel.cs** - Reactive properties and commands
✅ **Unit tests** - TestPlaybackWindow.cs with comprehensive coverage

## What You Can Test Right Now

### 1. Run Unit Tests

The easiest way to verify the implementation is to run the unit tests:

```bash
dotnet run -- --test
```

Or run the verification script directly in your code by temporarily modifying `Program.cs`:

```csharp
public static void Main(string[] args)
{
    // Temporarily add this for testing
    VerifyPlaybackWindow.Main(args).GetAwaiter().GetResult();
    return;
    
    // Original code...
}
```

This will output a detailed verification report showing all implemented features.

### 2. Visual Designer Preview (Limited)

You can open `Views/PlaybackWindow.axaml` in the Avalonia XAML designer to see:
- ✅ Layout structure
- ✅ Black background
- ✅ Subtitle area positioning
- ✅ Button placement
- ⚠️ Note: Video and visualizations won't render in designer

### 3. Build Verification

Verify the code compiles without errors:

```bash
dotnet build
```

Expected: Build succeeds with only warnings (no errors)

## What You CANNOT Test Yet (Requires Integration)

❌ **Opening the PlaybackWindow** - Not yet wired to MainWindow
❌ **Video playback** - Requires MediaPlayerController integration
❌ **Audio visualizations** - Requires AudioVisualizationEngine integration
❌ **Dual-screen mode** - Requires WindowManager integration (Task 19)
❌ **Subtitle display** - Requires subtitle file parsing
❌ **Cross-window communication** - Requires both windows running

## Next Steps for Full Testing

To enable full manual testing, you need to complete these tasks:

### Task 19: Create Control Window UI (Dual Screen Mode)
This will add the ability to:
- Switch between single and dual screen modes
- Open the PlaybackWindow from the main window
- Control playback from the control window

### Task 20: Integrate Window Manager
This will enable:
- Dual-screen coordination
- Window state management
- Cross-window message passing

### Task 21: Wire Up Playback Controls
This will connect:
- Play/pause/next/previous buttons
- Volume controls
- Playlist navigation
- MediaPlayerController to UI

## Temporary Testing Setup (Advanced)

If you want to test the PlaybackWindow in isolation right now, you can create a temporary test launcher:

### Create TestPlaybackWindowLauncher.cs:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using KaraokePlayer.Services;
using KaraokePlayer.ViewModels;
using KaraokePlayer.Views;
using LibVLCSharp.Shared;
using System;

namespace KaraokePlayer;

public class TestPlaybackWindowLauncher
{
    public static void Launch()
    {
        // Initialize LibVLC
        Core.Initialize();
        
        // Create mock services
        var mockController = new MockMediaPlayerController();
        
        // Create ViewModel
        var viewModel = new PlaybackWindowViewModel(mockController);
        
        // Set up test data
        viewModel.CurrentSong = new Models.MediaFile
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = "C:\\test\\video.mp4",
            Filename = "Test Video.mp4",
            Type = Models.MediaType.Video,
            Format = Models.MediaFormat.MP4,
            Metadata = new Models.MediaMetadata
            {
                Title = "Test Karaoke Song",
                Artist = "Test Artist",
                Duration = 240
            }
        };
        
        viewModel.HasMedia = true;
        viewModel.IsPlaying = true;
        
        // Create and show window
        var window = new PlaybackWindow
        {
            DataContext = viewModel
        };
        
        window.Show();
    }
}
```

### Modify Program.cs temporarily:

```csharp
public static void Main(string[] args)
{
    if (args.Contains("--test-playback"))
    {
        BuildAvaloniaApp()
            .AfterSetup(_ => TestPlaybackWindowLauncher.Launch())
            .StartWithClassicDesktopLifetime(args);
        return;
    }
    
    // Original code...
}
```

### Run with:

```bash
dotnet run -- --test-playback
```

This will open the PlaybackWindow in isolation where you can test:
- ✅ Window appearance
- ✅ Fullscreen toggle (F11)
- ✅ Keyboard shortcuts
- ✅ UI layout and styling
- ⚠️ Video won't play (no real media file)
- ⚠️ Visualizations won't animate (no audio data)

## What to Look For During Manual Testing

### Visual Checks
- [ ] Window opens with black background
- [ ] Fullscreen button visible in top-right
- [ ] "No media" message centered when no media loaded
- [ ] Subtitle area at bottom (when video playing)
- [ ] Clean, minimal UI with no distractions

### Keyboard Shortcuts
- [ ] F11 toggles fullscreen
- [ ] ESC exits fullscreen
- [ ] Double-click toggles fullscreen
- [ ] Space/arrows send commands (check console for messages)

### Fullscreen Behavior
- [ ] Window goes fullscreen on F11
- [ ] Fullscreen button hides in fullscreen mode
- [ ] Window state restored when exiting fullscreen
- [ ] ESC exits fullscreen properly

### Responsive Behavior
- [ ] Window can be resized
- [ ] Layout adapts to different sizes
- [ ] Minimum size enforced (800x600)
- [ ] Content scales appropriately

## Testing Checklist Summary

| Feature | Can Test Now | Requires Integration |
|---------|--------------|---------------------|
| UI Layout | ✅ Designer/Build | |
| Fullscreen Toggle | ✅ Isolated Test | |
| Keyboard Shortcuts | ✅ Isolated Test | |
| Video Playback | | ❌ Task 21 |
| Audio Visualization | | ❌ Task 21 |
| Subtitle Display | | ❌ Task 21 |
| Dual-Screen Mode | | ❌ Task 19, 20 |
| Cross-Window Control | | ❌ Task 19, 20 |
| Playlist Integration | | ❌ Task 21 |

## Recommended Testing Order

1. **Now**: Run unit tests to verify logic
2. **Now**: Build verification to ensure no errors
3. **Now**: Visual designer preview for layout
4. **After Task 19**: Test dual-screen mode switching
5. **After Task 20**: Test window management and coordination
6. **After Task 21**: Test full playback functionality
7. **After Task 21**: Test with real media files
8. **After Task 21**: Test audio visualizations
9. **After Task 21**: Test subtitle display

## Known Limitations

- **No real video playback yet**: MediaPlayerController needs integration
- **No audio visualization yet**: AudioVisualizationEngine needs integration
- **No subtitle parsing yet**: Subtitle file support needs implementation
- **No dual-screen coordination yet**: WindowManager needs integration
- **Mock data only**: Real media library not connected

## Questions?

If you want to test specific functionality, let me know and I can:
1. Create a more detailed isolated test setup
2. Mock additional services for testing
3. Add temporary UI elements for debugging
4. Create a demo mode with sample data

The implementation is solid and ready for integration - we just need to wire it up to the rest of the application!
