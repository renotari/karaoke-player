using System;
using System.IO;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Test class for MediaPlayerController
/// </summary>
public class MediaPlayerControllerTest
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== MediaPlayerController Tests ===\n");

        await TestInitialization();
        await TestVolumeControl();
        await TestAudioDevices();
        await TestSubtitleToggle();
        await TestStateManagement();
        await TestPreloading();
        await TestCrossfade();

        Console.WriteLine("\n=== All MediaPlayerController Tests Completed ===");
    }

    private static async Task TestInitialization()
    {
        Console.WriteLine("Test: Initialization");
        try
        {
            using var controller = new MediaPlayerController();
            
            Assert(controller.State == PlaybackState.Stopped, "Initial state should be Stopped");
            Assert(controller.Volume == 0.5f, "Initial volume should be 0.5");
            Assert(controller.SubtitlesEnabled == true, "Subtitles should be enabled by default");
            Assert(controller.CurrentMedia == null, "No media should be loaded initially");
            Assert(controller.CurrentTime == 0, "Current time should be 0");
            
            Console.WriteLine("✓ Initialization test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Initialization test failed: {ex.Message}\n");
        }
    }

    private static async Task TestVolumeControl()
    {
        Console.WriteLine("Test: Volume Control");
        try
        {
            using var controller = new MediaPlayerController();
            
            // Test setting volume
            controller.SetVolume(0.75f);
            Assert(controller.Volume == 0.75f, "Volume should be 0.75");
            
            controller.SetVolume(0.0f);
            Assert(controller.Volume == 0.0f, "Volume should be 0.0 (muted)");
            
            controller.SetVolume(1.0f);
            Assert(controller.Volume == 1.0f, "Volume should be 1.0 (max)");
            
            // Test invalid volume
            try
            {
                controller.SetVolume(1.5f);
                Console.WriteLine("✗ Should have thrown exception for invalid volume");
            }
            catch (ArgumentOutOfRangeException)
            {
                // Expected
            }
            
            Console.WriteLine("✓ Volume control test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Volume control test failed: {ex.Message}\n");
        }
    }

    private static async Task TestAudioDevices()
    {
        Console.WriteLine("Test: Audio Devices");
        try
        {
            using var controller = new MediaPlayerController();
            
            var devices = controller.GetAudioDevices();
            Assert(devices != null, "Audio devices list should not be null");
            
            Console.WriteLine($"  Found {devices.Count} audio device(s)");
            foreach (var device in devices)
            {
                Console.WriteLine($"    - {device.Name} (ID: {device.Id})");
            }
            
            Console.WriteLine("✓ Audio devices test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Audio devices test failed: {ex.Message}\n");
        }
    }

    private static async Task TestSubtitleToggle()
    {
        Console.WriteLine("Test: Subtitle Toggle");
        try
        {
            using var controller = new MediaPlayerController();
            
            // Test toggling subtitles
            controller.ToggleSubtitles(false);
            Assert(controller.SubtitlesEnabled == false, "Subtitles should be disabled");
            
            controller.ToggleSubtitles(true);
            Assert(controller.SubtitlesEnabled == true, "Subtitles should be enabled");
            
            Console.WriteLine("✓ Subtitle toggle test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Subtitle toggle test failed: {ex.Message}\n");
        }
    }

    private static async Task TestStateManagement()
    {
        Console.WriteLine("Test: State Management");
        try
        {
            using var controller = new MediaPlayerController();
            
            bool stateChanged = false;
            PlaybackState? newState = null;
            
            controller.StateChanged += (sender, args) =>
            {
                stateChanged = true;
                newState = args.NewState;
            };
            
            // Test stop (should not change state since already stopped)
            controller.Stop();
            Assert(controller.State == PlaybackState.Stopped, "State should remain Stopped");
            
            // Test pause when not playing (should not crash)
            controller.Pause();
            Assert(controller.State == PlaybackState.Stopped, "State should remain Stopped");
            
            Console.WriteLine("✓ State management test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ State management test failed: {ex.Message}\n");
        }
    }

    private static async Task TestPreloading()
    {
        Console.WriteLine("Test: Preloading");
        try
        {
            using var controller = new MediaPlayerController();
            
            // Create a test media file (won't actually play, just testing the API)
            var testMedia = new MediaFile
            {
                Id = "test-1",
                FilePath = "test.mp4",
                Filename = "test.mp4",
                Type = MediaType.Video,
                Format = MediaFormat.MP4
            };
            
            // Test preloading (will fail since file doesn't exist, but should not crash)
            try
            {
                await controller.PreloadNextAsync(testMedia);
                Console.WriteLine("  Preload attempted (file doesn't exist, but API works)");
            }
            catch
            {
                // Expected to fail since file doesn't exist
            }
            
            // Test null argument
            try
            {
                await controller.PreloadNextAsync(null!);
                Console.WriteLine("✗ Should have thrown exception for null media");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
            
            Console.WriteLine("✓ Preloading test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Preloading test failed: {ex.Message}\n");
        }
    }

    private static async Task TestCrossfade()
    {
        Console.WriteLine("Test: Crossfade");
        try
        {
            using var controller = new MediaPlayerController();
            
            // Test default crossfade state
            Assert(controller.CrossfadeEnabled == false, "Crossfade should be disabled by default");
            Assert(controller.CrossfadeDuration == 5, "Default crossfade duration should be 5 seconds");
            
            // Test enabling crossfade with valid duration
            controller.EnableCrossfade(true, 10);
            Assert(controller.CrossfadeEnabled == true, "Crossfade should be enabled");
            Assert(controller.CrossfadeDuration == 10, "Crossfade duration should be 10 seconds");
            
            // Test disabling crossfade
            controller.EnableCrossfade(false, 5);
            Assert(controller.CrossfadeEnabled == false, "Crossfade should be disabled");
            Assert(controller.CrossfadeDuration == 5, "Crossfade duration should be 5 seconds");
            
            // Test minimum duration (1 second)
            controller.EnableCrossfade(true, 1);
            Assert(controller.CrossfadeDuration == 1, "Crossfade duration should be 1 second");
            
            // Test maximum duration (20 seconds)
            controller.EnableCrossfade(true, 20);
            Assert(controller.CrossfadeDuration == 20, "Crossfade duration should be 20 seconds");
            
            // Test invalid duration (too low)
            try
            {
                controller.EnableCrossfade(true, 0);
                Console.WriteLine("✗ Should have thrown exception for duration < 1");
            }
            catch (ArgumentOutOfRangeException)
            {
                // Expected
            }
            
            // Test invalid duration (too high)
            try
            {
                controller.EnableCrossfade(true, 21);
                Console.WriteLine("✗ Should have thrown exception for duration > 20");
            }
            catch (ArgumentOutOfRangeException)
            {
                // Expected
            }
            
            Console.WriteLine("✓ Crossfade test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Crossfade test failed: {ex.Message}\n");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }
}
