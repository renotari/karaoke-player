using System;
using System.IO;
using System.Text.Json;
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

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public SettingsManager()
    {
        // Store settings in user's AppData directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDirectory = Path.Combine(appDataPath, "KaraokePlayer");
        Directory.CreateDirectory(appDirectory);
        
        _settingsFilePath = Path.Combine(appDirectory, "settings.json");
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

        _currentSettings = settings;
        _currentSettings.LastModified = DateTime.UtcNow;

        await SaveSettingsAsync();

        // Notify all properties changed
        this.RaisePropertyChanged(string.Empty);
    }

    /// <summary>
    /// Loads settings from JSON file
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                
                if (settings != null)
                {
                    // Validate loaded settings
                    ValidateAllSettings(settings);
                    _currentSettings = settings;
                    this.RaisePropertyChanged(string.Empty);
                }
            }
            else
            {
                // First run - create default settings
                _currentSettings = CreateDefaultSettings();
                await SaveSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            // Log error and use defaults
            Console.WriteLine($"Error loading settings: {ex.Message}");
            _currentSettings = CreateDefaultSettings();
            await SaveSettingsAsync();
        }
    }

    /// <summary>
    /// Saves settings to JSON file
    /// </summary>
    public async Task SaveSettingsAsync()
    {
        try
        {
            _currentSettings.LastModified = DateTime.UtcNow;
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(_currentSettings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
            throw;
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
                    var validStyles = new[] { "bars", "waveform", "circular", "particles" };
                    if (!Array.Exists(validStyles, s => s.Equals(style, StringComparison.OrdinalIgnoreCase)))
                    {
                        errorMessage = "Visualization style must be one of: bars, waveform, circular, particles";
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

        // Validate media directory
        if (!ValidateSetting(nameof(AppSettings.MediaDirectory), settings.MediaDirectory, out error))
        {
            throw new ArgumentException(error);
        }

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
