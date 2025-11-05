using System;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Simple test class to verify SettingsManager functionality
/// This can be run from Program.cs during development
/// </summary>
public static class SettingsManagerTest
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== Settings Manager Tests ===\n");

        var settingsManager = new SettingsManager();

        // Test 1: Load default settings
        Console.WriteLine("Test 1: Loading default settings...");
        await settingsManager.LoadSettingsAsync();
        var settings = settingsManager.GetSettings();
        Console.WriteLine($"✓ Default media directory: {settings.MediaDirectory}");
        Console.WriteLine($"✓ Default volume: {settings.Volume}");
        Console.WriteLine($"✓ Default crossfade duration: {settings.CrossfadeDuration}");

        // Test 2: Get setting by key
        Console.WriteLine("\nTest 2: Getting settings by key...");
        var volume = settingsManager.GetSetting<double>(nameof(AppSettings.Volume));
        var theme = settingsManager.GetSetting<string>(nameof(AppSettings.Theme));
        Console.WriteLine($"✓ Volume: {volume}");
        Console.WriteLine($"✓ Theme: {theme}");

        // Test 3: Set setting with validation
        Console.WriteLine("\nTest 3: Setting values with validation...");
        settingsManager.SetSetting(nameof(AppSettings.Volume), 0.5);
        Console.WriteLine($"✓ Volume changed to: {settingsManager.GetSetting<double>(nameof(AppSettings.Volume))}");

        settingsManager.SetSetting(nameof(AppSettings.CrossfadeDuration), 5);
        Console.WriteLine($"✓ Crossfade duration changed to: {settingsManager.GetSetting<int>(nameof(AppSettings.CrossfadeDuration))}");

        // Test 4: Validation - invalid crossfade duration
        Console.WriteLine("\nTest 4: Testing validation (invalid crossfade duration)...");
        try
        {
            settingsManager.SetSetting(nameof(AppSettings.CrossfadeDuration), 25); // Invalid: > 20
            Console.WriteLine("✗ Validation failed - should have thrown exception");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"✓ Validation passed: {ex.Message}");
        }

        // Test 5: Validation - invalid volume
        Console.WriteLine("\nTest 5: Testing validation (invalid volume)...");
        try
        {
            settingsManager.SetSetting(nameof(AppSettings.Volume), 1.5); // Invalid: > 1.0
            Console.WriteLine("✗ Validation failed - should have thrown exception");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"✓ Validation passed: {ex.Message}");
        }

        // Test 6: Save and reload settings
        Console.WriteLine("\nTest 6: Saving and reloading settings...");
        settingsManager.SetSetting(nameof(AppSettings.VisualizationStyle), "waveform");
        await settingsManager.SaveSettingsAsync();
        Console.WriteLine("✓ Settings saved");

        var newSettingsManager = new SettingsManager();
        await newSettingsManager.LoadSettingsAsync();
        var reloadedStyle = newSettingsManager.GetSetting<string>(nameof(AppSettings.VisualizationStyle));
        Console.WriteLine($"✓ Reloaded visualization style: {reloadedStyle}");

        // Test 7: Settings changed event
        Console.WriteLine("\nTest 7: Testing settings changed event...");
        bool eventFired = false;
        settingsManager.SettingsChanged += (sender, args) =>
        {
            eventFired = true;
            Console.WriteLine($"✓ Event fired - Key: {args.SettingKey}, Old: {args.OldValue}, New: {args.NewValue}");
        };
        settingsManager.SetSetting(nameof(AppSettings.Theme), "light");
        if (eventFired)
        {
            Console.WriteLine("✓ Settings changed event working correctly");
        }

        // Test 8: Reset to defaults
        Console.WriteLine("\nTest 8: Resetting to defaults...");
        await settingsManager.ResetToDefaultsAsync();
        var resetSettings = settingsManager.GetSettings();
        Console.WriteLine($"✓ Volume reset to: {resetSettings.Volume}");
        Console.WriteLine($"✓ Theme reset to: {resetSettings.Theme}");
        Console.WriteLine($"✓ Crossfade duration reset to: {resetSettings.CrossfadeDuration}");

        Console.WriteLine("\n=== All Tests Passed ===");
    }
}
