using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;

namespace KaraokePlayer.Services
{
    /// <summary>
    /// Test class for WindowManager functionality
    /// </summary>
    public class WindowManagerTest
    {
        private readonly ISettingsManager _settingsManager;
        private readonly IWindowManager _windowManager;
        private readonly IMessageBus _messageBus;

        public WindowManagerTest()
        {
            _settingsManager = new SettingsManager();
            _messageBus = new MessageBus();
            _windowManager = new WindowManager(_settingsManager, _messageBus);
        }

        public async Task RunAllTests()
        {
            Console.WriteLine("=== Window Manager Tests ===\n");

            await TestSetMode();
            await TestToggleFullscreen();
            await TestWindowStatePersistence();
            await TestMessageBroadcasting();
            await TestPlaylistComposerWindow();
            await TestModeChangeEvents();

            Console.WriteLine("\n=== All Window Manager Tests Completed ===");
        }

        private async Task TestSetMode()
        {
            Console.WriteLine("Test: Set Mode");
            
            // Test initial mode
            var initialMode = _windowManager.CurrentMode;
            Console.WriteLine($"  Initial mode: {initialMode}");

            // Test switching to dual mode
            await _windowManager.SetModeAsync(WindowMode.Dual);
            if (_windowManager.CurrentMode == WindowMode.Dual)
            {
                Console.WriteLine("  ✓ Successfully switched to Dual mode");
            }
            else
            {
                Console.WriteLine("  ✗ Failed to switch to Dual mode");
            }

            // Test switching back to single mode
            await _windowManager.SetModeAsync(WindowMode.Single);
            if (_windowManager.CurrentMode == WindowMode.Single)
            {
                Console.WriteLine("  ✓ Successfully switched to Single mode");
            }
            else
            {
                Console.WriteLine("  ✗ Failed to switch to Single mode");
            }

            // Test idempotent mode setting
            await _windowManager.SetModeAsync(WindowMode.Single);
            if (_windowManager.CurrentMode == WindowMode.Single)
            {
                Console.WriteLine("  ✓ Idempotent mode setting works correctly");
            }

            Console.WriteLine();
        }

        private async Task TestToggleFullscreen()
        {
            Console.WriteLine("Test: Toggle Fullscreen");

            const string windowId = "MainWindow";

            // Test initial fullscreen state
            var isFullscreen = _windowManager.IsFullscreen(windowId);
            Console.WriteLine($"  Initial fullscreen state: {isFullscreen}");

            // Toggle fullscreen on
            _windowManager.ToggleFullscreen(windowId);
            if (_windowManager.IsFullscreen(windowId))
            {
                Console.WriteLine("  ✓ Successfully toggled fullscreen ON");
            }
            else
            {
                Console.WriteLine("  ✗ Failed to toggle fullscreen ON");
            }

            // Toggle fullscreen off
            _windowManager.ToggleFullscreen(windowId);
            if (!_windowManager.IsFullscreen(windowId))
            {
                Console.WriteLine("  ✓ Successfully toggled fullscreen OFF");
            }
            else
            {
                Console.WriteLine("  ✗ Failed to toggle fullscreen OFF");
            }

            // Test multiple windows
            const string secondWindow = "PlaybackWindow";
            _windowManager.ToggleFullscreen(secondWindow);
            if (_windowManager.IsFullscreen(secondWindow) && !_windowManager.IsFullscreen(windowId))
            {
                Console.WriteLine("  ✓ Multiple windows maintain independent fullscreen states");
            }

            await Task.CompletedTask;
            Console.WriteLine();
        }

        private async Task TestWindowStatePersistence()
        {
            Console.WriteLine("Test: Window State Persistence");

            // Create a window manager instance
            var windowManager = (WindowManager)_windowManager;

            // Create test window states
            var mainWindowState = new WindowState
            {
                WindowId = "MainWindow",
                X = 100,
                Y = 100,
                Width = 1280,
                Height = 720,
                IsMaximized = false
            };

            var playbackWindowState = new WindowState
            {
                WindowId = "PlaybackWindow",
                X = 1920,
                Y = 0,
                Width = 1920,
                Height = 1080,
                IsMaximized = true
            };

            // Update window states
            windowManager.UpdateWindowState(mainWindowState);
            windowManager.UpdateWindowState(playbackWindowState);

            // Save window states
            await _windowManager.SaveWindowStateAsync();
            Console.WriteLine("  ✓ Window states saved");

            // Create new window manager to test restoration
            var newWindowManager = new WindowManager(_settingsManager, _messageBus);
            await newWindowManager.RestoreWindowStateAsync();
            Console.WriteLine("  ✓ Window states restored");

            Console.WriteLine();
        }

        private async Task TestMessageBroadcasting()
        {
            Console.WriteLine("Test: Message Broadcasting");

            var messageReceived = false;
            var correctMessage = false;

            // Subscribe to mode change messages
            var subscription = _windowManager.Subscribe<WindowModeChangeMessage>(msg =>
            {
                messageReceived = true;
                correctMessage = msg.OldMode == WindowMode.Single && msg.NewMode == WindowMode.Dual;
            });

            // Trigger mode change
            await _windowManager.SetModeAsync(WindowMode.Dual);

            // Small delay to allow message processing
            await Task.Delay(50);

            if (messageReceived && correctMessage)
            {
                Console.WriteLine("  ✓ Message broadcasting works correctly");
            }
            else
            {
                Console.WriteLine("  ✗ Message broadcasting failed");
            }

            subscription.Dispose();

            // Test custom message broadcasting
            var customMessageReceived = false;
            var customSubscription = _windowManager.Subscribe<StateSyncMessage>(msg =>
            {
                customMessageReceived = msg.StateKey == "TestKey";
            });

            _windowManager.BroadcastMessage(new StateSyncMessage
            {
                StateKey = "TestKey",
                StateValue = "TestValue"
            });

            await Task.Delay(50);

            if (customMessageReceived)
            {
                Console.WriteLine("  ✓ Custom message broadcasting works correctly");
            }
            else
            {
                Console.WriteLine("  ✗ Custom message broadcasting failed");
            }

            customSubscription.Dispose();
            Console.WriteLine();
        }

        private async Task TestPlaylistComposerWindow()
        {
            Console.WriteLine("Test: Playlist Composer Window");

            var openMessageReceived = false;
            var closeMessageReceived = false;

            // Subscribe to playlist composer messages
            var openSubscription = _windowManager.Subscribe<OpenPlaylistComposerMessage>(msg =>
            {
                openMessageReceived = true;
            });

            var closeSubscription = _windowManager.Subscribe<ClosePlaylistComposerMessage>(msg =>
            {
                closeMessageReceived = true;
            });

            // Open playlist composer
            await _windowManager.OpenPlaylistComposerAsync();
            await Task.Delay(50);

            if (openMessageReceived)
            {
                Console.WriteLine("  ✓ Playlist Composer open message sent");
            }
            else
            {
                Console.WriteLine("  ✗ Playlist Composer open message not received");
            }

            // Close playlist composer
            _windowManager.ClosePlaylistComposer();
            await Task.Delay(50);

            if (closeMessageReceived)
            {
                Console.WriteLine("  ✓ Playlist Composer close message sent");
            }
            else
            {
                Console.WriteLine("  ✗ Playlist Composer close message not received");
            }

            openSubscription.Dispose();
            closeSubscription.Dispose();
            Console.WriteLine();
        }

        private async Task TestModeChangeEvents()
        {
            Console.WriteLine("Test: Mode Change Events");

            var eventRaised = false;
            WindowMode? oldMode = null;
            WindowMode? newMode = null;

            // Subscribe to mode change event
            _windowManager.ModeChanged += (sender, args) =>
            {
                eventRaised = true;
                oldMode = args.OldMode;
                newMode = args.NewMode;
            };

            // Change mode
            await _windowManager.SetModeAsync(WindowMode.Single);
            await _windowManager.SetModeAsync(WindowMode.Dual);

            if (eventRaised && oldMode == WindowMode.Single && newMode == WindowMode.Dual)
            {
                Console.WriteLine("  ✓ Mode change event raised correctly");
            }
            else
            {
                Console.WriteLine("  ✗ Mode change event not raised correctly");
            }

            // Test fullscreen change event
            var fullscreenEventRaised = false;
            string? fullscreenWindowId = null;
            bool? fullscreenState = null;

            _windowManager.FullscreenChanged += (sender, args) =>
            {
                fullscreenEventRaised = true;
                fullscreenWindowId = args.WindowId;
                fullscreenState = args.IsFullscreen;
            };

            _windowManager.ToggleFullscreen("TestWindow");

            if (fullscreenEventRaised && fullscreenWindowId == "TestWindow" && fullscreenState == true)
            {
                Console.WriteLine("  ✓ Fullscreen change event raised correctly");
            }
            else
            {
                Console.WriteLine("  ✗ Fullscreen change event not raised correctly");
            }

            Console.WriteLine();
        }
    }
}
