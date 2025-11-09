using System;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer;

/// <summary>
/// Verification script for SettingsViewModel
/// </summary>
public static class VerifySettingsViewModel
{
    public static async Task Run()
    {
        Console.WriteLine("=== SettingsViewModel Verification ===\n");

        try
        {
            // Create a settings manager
            var settingsManager = new SettingsManager();
            await settingsManager.LoadSettingsAsync();

            // Create the ViewModel
            var viewModel = new SettingsViewModel(settingsManager, null, null);

            Console.WriteLine("✓ SettingsViewModel created successfully");
            Console.WriteLine($"  - Media Directory: {viewModel.MediaDirectory}");
            Console.WriteLine($"  - Volume: {viewModel.VolumePercent}%");
            Console.WriteLine($"  - Crossfade Enabled: {viewModel.CrossfadeEnabled}");
            Console.WriteLine($"  - Crossfade Duration: {viewModel.CrossfadeDuration}s");
            Console.WriteLine($"  - Font Size: {viewModel.FontSize}");
            Console.WriteLine($"  - Preload Buffer: {viewModel.PreloadBufferSize}MB");
            Console.WriteLine($"  - Cache Size: {viewModel.CacheSize}MB");

            // Test validation
            Console.WriteLine("\n--- Testing Validation ---");
            
            // Test crossfade duration validation
            viewModel.CrossfadeDuration = 0; // Invalid
            if (!string.IsNullOrEmpty(viewModel.CrossfadeDurationError))
            {
                Console.WriteLine($"✓ Crossfade duration validation works: {viewModel.CrossfadeDurationError}");
            }
            else
            {
                Console.WriteLine("✗ Crossfade duration validation failed");
            }

            viewModel.CrossfadeDuration = 5; // Valid
            if (string.IsNullOrEmpty(viewModel.CrossfadeDurationError))
            {
                Console.WriteLine("✓ Crossfade duration accepts valid value");
            }
            else
            {
                Console.WriteLine("✗ Crossfade duration rejects valid value");
            }

            // Test font size validation
            viewModel.FontSize = 50; // Invalid
            if (!string.IsNullOrEmpty(viewModel.FontSizeError))
            {
                Console.WriteLine($"✓ Font size validation works: {viewModel.FontSizeError}");
            }
            else
            {
                Console.WriteLine("✗ Font size validation failed");
            }

            viewModel.FontSize = 16; // Valid
            if (string.IsNullOrEmpty(viewModel.FontSizeError))
            {
                Console.WriteLine("✓ Font size accepts valid value");
            }
            else
            {
                Console.WriteLine("✗ Font size rejects valid value");
            }

            // Test media directory validation
            viewModel.MediaDirectory = ""; // Invalid
            if (!string.IsNullOrEmpty(viewModel.MediaDirectoryError))
            {
                Console.WriteLine($"✓ Media directory validation works: {viewModel.MediaDirectoryError}");
            }
            else
            {
                Console.WriteLine("✗ Media directory validation failed");
            }

            viewModel.MediaDirectory = "C:\\Music"; // Valid
            if (string.IsNullOrEmpty(viewModel.MediaDirectoryError))
            {
                Console.WriteLine("✓ Media directory accepts valid value");
            }
            else
            {
                Console.WriteLine("✗ Media directory rejects valid value");
            }

            // Test volume clamping
            Console.WriteLine("\n--- Testing Volume Clamping ---");
            viewModel.VolumePercent = 150;
            if (viewModel.VolumePercent == 100)
            {
                Console.WriteLine("✓ Volume clamped to maximum (100)");
            }
            else
            {
                Console.WriteLine($"✗ Volume not clamped correctly: {viewModel.VolumePercent}");
            }

            viewModel.VolumePercent = -10;
            if (viewModel.VolumePercent == 0)
            {
                Console.WriteLine("✓ Volume clamped to minimum (0)");
            }
            else
            {
                Console.WriteLine($"✗ Volume not clamped correctly: {viewModel.VolumePercent}");
            }

            // Test HasValidationErrors property
            Console.WriteLine("\n--- Testing HasValidationErrors ---");
            viewModel.CrossfadeDuration = 0; // Invalid
            if (viewModel.HasValidationErrors)
            {
                Console.WriteLine("✓ HasValidationErrors returns true when errors exist");
            }
            else
            {
                Console.WriteLine("✗ HasValidationErrors should return true");
            }

            viewModel.CrossfadeDuration = 5; // Valid
            if (!viewModel.HasValidationErrors)
            {
                Console.WriteLine("✓ HasValidationErrors returns false when no errors");
            }
            else
            {
                Console.WriteLine("✗ HasValidationErrors should return false");
            }

            // Test Apply command
            Console.WriteLine("\n--- Testing Apply Command ---");
            viewModel.VolumePercent = 75;
            viewModel.CrossfadeDuration = 7;
            viewModel.ApplyCommand.Execute().Subscribe();
            
            var updatedSettings = settingsManager.GetSettings();
            if (Math.Abs(updatedSettings.Volume - 0.75) < 0.01 && updatedSettings.CrossfadeDuration == 7)
            {
                Console.WriteLine("✓ Apply command saves settings correctly");
            }
            else
            {
                Console.WriteLine("✗ Apply command did not save settings correctly");
            }

            // Test Reset to Defaults
            Console.WriteLine("\n--- Testing Reset to Defaults ---");
            viewModel.VolumePercent = 50;
            viewModel.CrossfadeDuration = 10;
            viewModel.ResetToDefaultsCommand.Execute().Subscribe();
            
            if (viewModel.VolumePercent == 80 && viewModel.CrossfadeDuration == 3)
            {
                Console.WriteLine("✓ Reset to defaults works correctly");
            }
            else
            {
                Console.WriteLine($"✗ Reset to defaults failed: Volume={viewModel.VolumePercent}, Crossfade={viewModel.CrossfadeDuration}");
            }

            // Test audio devices collection
            Console.WriteLine("\n--- Testing Audio Devices ---");
            if (viewModel.AudioDevices.Count > 0)
            {
                Console.WriteLine($"✓ Audio devices loaded: {viewModel.AudioDevices.Count} device(s)");
                foreach (var device in viewModel.AudioDevices)
                {
                    Console.WriteLine($"  - {device}");
                }
            }
            else
            {
                Console.WriteLine("✗ No audio devices loaded");
            }

            // Test keyboard shortcuts
            Console.WriteLine("\n--- Testing Keyboard Shortcuts ---");
            if (viewModel.KeyboardShortcuts.Count > 0)
            {
                Console.WriteLine($"✓ Keyboard shortcuts loaded: {viewModel.KeyboardShortcuts.Count} shortcut(s)");
                Console.WriteLine($"  - First shortcut: {viewModel.KeyboardShortcuts[0].Action} = {viewModel.KeyboardShortcuts[0].Shortcut}");
            }
            else
            {
                Console.WriteLine("✗ No keyboard shortcuts loaded");
            }

            Console.WriteLine("\n=== All Verifications Complete ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error during verification: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
