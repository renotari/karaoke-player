using System;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for managing application settings
/// </summary>
public interface ISettingsManager
{
    /// <summary>
    /// Gets a setting value by key with type safety
    /// </summary>
    T GetSetting<T>(string key);

    /// <summary>
    /// Sets a setting value by key with type safety
    /// </summary>
    void SetSetting<T>(string key, T value);

    /// <summary>
    /// Gets the current AppSettings object
    /// </summary>
    AppSettings GetSettings();

    /// <summary>
    /// Updates the AppSettings object
    /// </summary>
    Task UpdateSettingsAsync(AppSettings settings);

    /// <summary>
    /// Loads settings from storage
    /// </summary>
    Task LoadSettingsAsync();

    /// <summary>
    /// Saves settings to storage
    /// </summary>
    Task SaveSettingsAsync();

    /// <summary>
    /// Resets all settings to default values
    /// </summary>
    Task ResetToDefaultsAsync();

    /// <summary>
    /// Validates a setting value
    /// </summary>
    bool ValidateSetting<T>(string key, T value, out string? errorMessage);

    /// <summary>
    /// Event raised when settings change
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
}

/// <summary>
/// Event arguments for settings changes
/// </summary>
public class SettingsChangedEventArgs : EventArgs
{
    public string SettingKey { get; set; } = string.Empty;
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
}
