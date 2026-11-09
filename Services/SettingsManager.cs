using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using ReactiveUI;

namespace KaraokePlayer.Services;

/// <summary>
/// Manages application settings with persistence and validation
/// </summary>
public class SettingsManager : ReactiveObject, ISettingsManager
{
    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public SettingsManager()
    {
        // Store settings in user's AppData directory
        _settingsFilePath = AppPaths.SettingsFile;
        _currentSettings = CreateDefaultSettings();
    }

    /// <summary>
    /// Gets a setting value by key with type safety
    /// </summary>
    public T GetSetting<T>(string key)
    {
        var property = typeof(AppSettings).GetProperty(key);
        if (property == null)
        {
            throw new ArgumentException($"Setting key '{key}' does not exist", nameof(key));
        }

        var value = property.GetValue(_currentSettings);
        if (value is T typedValue)
        {
            return typedValue;
        }

        throw new InvalidCastException($"Setting '{key}' cannot be cast to type {typeof(T).Name}");
    }

    /// <summary>
    /// Sets a setting value by key with type safety and validation
    /// </summary>
    public void SetSetting<T>(string key, T value)
    {
        var property = typeof(AppSettings).GetProperty(key);
        if (property == null)
        {
            throw new ArgumentException($"Setting key '{key}' does not exist", nameof(key));
        }

        // Validate the setting
        if (!ValidateSetting(key, value, out var errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(value));
        }

        var oldValue = property.GetValue(_currentSettings);
        property.SetValue(_currentSettings, value);
        _currentSettings.LastModified = DateTime.UtcNow;

        // Raise property changed for ReactiveUI
        this.RaisePropertyChanged(key);

        // Raise settings changed event
        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
        {
            SettingKey = key,
            OldValue = oldValue,
            NewValue = value
        });
    }

    /// <summary>
    /// Gets the current AppSettings object
    /// </summary>
    public AppSettings GetSettings()
    {
        return _currentSettings;
    }

    /// <summary>
    /// Updates the entire AppSettings object
    /// </summary>
    public async Task UpdateSettingsAsync(AppSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        // Validate all settings
        ValidateAllSettings(settings);

        // Saved first: if the file cannot be written the active settings stay as they were
        settings.LastModified = DateTime.UtcNow;
        await WriteSettingsFileAsync(settings);
        _currentSettings = settings;

        // Notify all properties changed
        this.RaisePropertyChanged(string.Empty);

        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
        {
            SettingKey = "All",
            OldValue = null,
            NewValue = _currentSettings
        });
    }

    /// <summary>
    /// Loads settings from the JSON file. A damaged file is never thrown away:
    /// unreadable JSON is kept as a backup copy before defaults are used, and a
    /// single invalid value is reset on its own (the other preferences are kept).
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            // First run - create default settings
            _currentSettings = CreateDefaultSettings();
            await TrySaveAsync();
            return;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(_settingsFilePath);
        }
        catch (Exception ex)
        {
            // Locked or unreadable right now: use defaults for this session, keep a copy
            Console.WriteLine($"Error reading settings: {ex.Message}");
            BackUpSettingsFile("unreadable");
            _currentSettings = CreateDefaultSettings();
            return;
        }

        AppSettings? settings = null;
        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Settings file is not valid JSON: {ex.Message}");
        }

        if (settings == null)
        {
            BackUpSettingsFile("invalid");
            _currentSettings = CreateDefaultSettings();
            await TrySaveAsync();
            return;
        }

        var repaired = RepairSettings(settings);
        _currentSettings = settings;
        this.RaisePropertyChanged(string.Empty);

        if (repaired.Count > 0)
        {
            Console.WriteLine($"Invalid settings reset to their defaults: {string.Join(", ", repaired)}");
            await TrySaveAsync();
        }
    }

    /// <summary>Copies the settings file aside (settings.json.invalid-20261109-2130).</summary>
    private void BackUpSettingsFile(string reason)
    {
        try
        {
            File.Copy(_settingsFilePath, $"{_settingsFilePath}.{reason}-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not back up the settings file: {ex.Message}");
        }
    }

    /// <summary>
    /// Puts back the default value of every invalid field; returns their names.
    /// An empty media folder is valid (not chosen yet).
    /// </summary>
    private List<string> RepairSettings(AppSettings settings)
    {
        var defaults = CreateDefaultSettings();
        var repaired = new List<string>();

        void Check<T>(string name, T value, Action reset)
        {
            if (!ValidateSetting(name, value, out _))
            {
                reset();
                repaired.Add(name);
            }
        }

        Check(nameof(AppSettings.Volume), settings.Volume, () => settings.Volume = defaults.Volume);
        Check(nameof(AppSettings.CrossfadeDuration), settings.CrossfadeDuration, () => settings.CrossfadeDuration = defaults.CrossfadeDuration);
        Check(nameof(AppSettings.FontSize), settings.FontSize, () => settings.FontSize = defaults.FontSize);
        Check(nameof(AppSettings.PreloadBufferSize), settings.PreloadBufferSize, () => settings.PreloadBufferSize = defaults.PreloadBufferSize);
        Check(nameof(AppSettings.CacheSize), settings.CacheSize, () => settings.CacheSize = defaults.CacheSize);
        Check(nameof(AppSettings.VisualizationStyle), settings.VisualizationStyle, () => settings.VisualizationStyle = defaults.VisualizationStyle);
        Check(nameof(AppSettings.Theme), settings.Theme, () => settings.Theme = defaults.Theme);

        if (settings.MediaDirectory == null)
        {
            settings.MediaDirectory = string.Empty;
            repaired.Add(nameof(AppSettings.MediaDirectory));
        }
        if (settings.YouTubeMaxHeight is < 144 or > 4320)
        {
            settings.YouTubeMaxHeight = defaults.YouTubeMaxHeight;
            repaired.Add(nameof(AppSettings.YouTubeMaxHeight));
        }

        return repaired;
    }

    private async Task TrySaveAsync()
    {
        try { await SaveSettingsAsync(); }
        catch { /* already logged; the settings in memory still work */ }
    }

    /// <summary>
    /// Saves settings to JSON file
    /// </summary>
    public async Task SaveSettingsAsync()
    {
        _currentSettings.LastModified = DateTime.UtcNow;
        await WriteSettingsFileAsync(_currentSettings);
    }

    /// <summary>
    /// Writes the settings: one save at a time, to a temporary file that then
    /// replaces settings.json (a failed write keeps the previous file).
    /// </summary>
    private async Task WriteSettingsFileAsync(AppSettings settings)
    {
        await _saveLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _settingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _settingsFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Resets all settings to default values
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        _currentSettings = CreateDefaultSettings();
        await SaveSettingsAsync();
        this.RaisePropertyChanged(string.Empty);
        
        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
        {
            SettingKey = "All",
            OldValue = null,
            NewValue = _currentSettings
        });
    }

    /// <summary>
    /// Validates a setting value
    /// </summary>
    public bool ValidateSetting<T>(string key, T value, out string? errorMessage)
    {
        errorMessage = null;

        switch (key)
        {
            case nameof(AppSettings.Volume):
                if (value is double volume)
                {
                    if (volume < 0.0 || volume > 1.0)
                    {
                        errorMessage = "Volume must be between 0.0 and 1.0";
                        return false;
                    }
                }
                break;

            case nameof(AppSettings.CrossfadeDuration):
                if (value is int duration)
                {
                    if (duration < 1 || duration > 20)
                    {
                        errorMessage = "Crossfade duration must be between 1 and 20 seconds";
                        return false;
                    }
                }
                break;

            case nameof(AppSettings.MediaDirectory):
                if (value is string directory)
                {
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        errorMessage = "Media directory cannot be empty";
                        return false;
                    }
                }
                break;

            case nameof(AppSettings.FontSize):
                if (value is int fontSize)
                {
                    if (fontSize < 8 || fontSize > 32)
                    {
                        errorMessage = "Font size must be between 8 and 32";
                        return false;
                    }
                }
                break;

            case nameof(AppSettings.PreloadBufferSize):
                if (value is int bufferSize)
                {
                    if (bufferSize < 10 || bufferSize > 500)
                    {
                        errorMessage = "Preload buffer size must be between 10 and 500 MB";
                        return false;
                    }
                }
                break;

            case nameof(AppSettings.CacheSize):
                if (value is int cacheSize)
                {
                    if (cacheSize < 100 || cacheSize > 5000)
                    {
                        errorMessage = "Cache size must be between 100 and 5000 MB";
                        return false;
                    }
                }
                break;

            case nameof(AppSettings.VisualizationStyle):
                if (value is string style)
                {
                    var validStyles = new[] { "bars", "waveform", "circular", "particles", "none" };
                    if (!Array.Exists(validStyles, s => s.Equals(style, StringComparison.OrdinalIgnoreCase)))
                    {
                        errorMessage = "Visualization style must be one of: bars, waveform, circular, particles, none";
                        return false;
                    }
                }
                break;

            case nameof(AppSettings.Theme):
                if (value is string theme)
                {
                    var validThemes = new[] { "dark", "light" };
                    if (!Array.Exists(validThemes, t => t.Equals(theme, StringComparison.OrdinalIgnoreCase)))
                    {
                        errorMessage = "Theme must be either 'dark' or 'light'";
                        return false;
                    }
                }
                break;
        }

        return true;
    }

    /// <summary>
    /// Creates default settings
    /// </summary>
    private static AppSettings CreateDefaultSettings()
    {
        var userMediaPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        
        return new AppSettings
        {
            Id = "default",
            MediaDirectory = userMediaPath,
            DisplayMode = DisplayMode.Single,
            Volume = 0.8,
            AudioBoostEnabled = false,
            AudioOutputDevice = "default",
            CrossfadeEnabled = false,
            CrossfadeDuration = 3,
            AutoPlayEnabled = true,
            ShuffleMode = false,
            VisualizationStyle = "bars",
            Theme = "dark",
            FontSize = 14,
            PreloadBufferSize = 50,
            CacheSize = 500,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Validates all settings in an AppSettings object
    /// </summary>
    private void ValidateAllSettings(AppSettings settings)
    {
        // Validate volume
        if (!ValidateSetting(nameof(AppSettings.Volume), settings.Volume, out var error))
        {
            throw new ArgumentException(error);
        }

        // Validate crossfade duration
        if (!ValidateSetting(nameof(AppSettings.CrossfadeDuration), settings.CrossfadeDuration, out error))
        {
            throw new ArgumentException(error);
        }

        // An empty media directory is allowed (not chosen yet): the folder picker and
        // the Settings dialog make sure the user picks one

        // Validate font size
        if (!ValidateSetting(nameof(AppSettings.FontSize), settings.FontSize, out error))
        {
            throw new ArgumentException(error);
        }

        // Validate preload buffer size
        if (!ValidateSetting(nameof(AppSettings.PreloadBufferSize), settings.PreloadBufferSize, out error))
        {
            throw new ArgumentException(error);
        }

        // Validate cache size
        if (!ValidateSetting(nameof(AppSettings.CacheSize), settings.CacheSize, out error))
        {
            throw new ArgumentException(error);
        }

        // Validate visualization style
        if (!ValidateSetting(nameof(AppSettings.VisualizationStyle), settings.VisualizationStyle, out error))
        {
            throw new ArgumentException(error);
        }

        // Validate theme
        if (!ValidateSetting(nameof(AppSettings.Theme), settings.Theme, out error))
        {
            throw new ArgumentException(error);
        }
    }
}
