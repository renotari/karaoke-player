using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsManager _settingsManager;
    private readonly IMediaPlayerController? _mediaPlayerController;
    private readonly Window? _owner;
    private AppSettings _originalSettings;
    private AppSettings _workingSettings;
    private bool _disposed;

    // General Tab
    [ObservableProperty]
    private string _mediaDirectory = string.Empty;
    
    [ObservableProperty]
    private int _displayModeIndex;
    
    [ObservableProperty]
    private bool _autoPlayEnabled;
    
    [ObservableProperty]
    private bool _shuffleMode;

    // Audio Tab
    [ObservableProperty]
    private double _volumePercent;
    
    [ObservableProperty]
    private bool _audioBoostEnabled;
    
    [ObservableProperty]
    private string _selectedAudioDevice = "default";
    
    [ObservableProperty]
    private bool _crossfadeEnabled;
    
    [ObservableProperty]
    private int _crossfadeDuration;

    // Display Tab
    [ObservableProperty]
    private int _themeIndex;
    
    [ObservableProperty]
    private int _fontSize;
    
    [ObservableProperty]
    private int _visualizationStyleIndex;

    // Performance Tab
    [ObservableProperty]
    private int _preloadBufferSize;
    
    [ObservableProperty]
    private int _cacheSize;

    // Validation errors
    [ObservableProperty]
    private string? _mediaDirectoryError;
    
    [ObservableProperty]
    private string? _crossfadeDurationError;
    
    [ObservableProperty]
    private string? _fontSizeError;
    
    [ObservableProperty]
    private string? _preloadBufferSizeError;
    
    [ObservableProperty]
    private string? _cacheSizeError;

    public SettingsViewModel()
    {
        // Design-time constructor - initialize all required fields
        // This ensures that XAML designer doesn't crash on null references

        _settingsManager = new SettingsManager();
        _mediaPlayerController = null;
        _owner = null;

        // Initialize with default settings
        _originalSettings = new AppSettings();
        _workingSettings = new AppSettings(); // Don't call CloneSettings in design-time

        // Collections are already initialized inline
        // Add a default audio device for design-time
        try
        {
            AudioDevices.Add("Default Audio Device");
            _selectedAudioDevice = "Default Audio Device";
        }
        catch
        {
            // Ignore collection errors in design-time
        }

        // Initialize default values for properties to avoid null reference exceptions
        _mediaDirectory = string.Empty;
        _volumePercent = 100;
        _crossfadeDuration = 3;
        _fontSize = 14;
        _preloadBufferSize = 30;
        _cacheSize = 500;

        // Commands are auto-generated and available in design-time
    }

    public SettingsViewModel(ISettingsManager? settingsManager, IMediaPlayerController? mediaPlayerController, Window? owner)
    {
        // Ensure Logs directory exists before any file operations
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KaraokePlayer", "Logs");
        Directory.CreateDirectory(logDir);

        var logPath = Path.Combine(logDir, "settings-debug.log");

        try
        {
            File.AppendAllText(logPath, "SettingsViewModel: Constructor called\n");
            Console.WriteLine("SettingsViewModel: Starting initialization...");
            
            File.AppendAllText(logPath, "SettingsViewModel: Checking parameters...\n");
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _mediaPlayerController = mediaPlayerController;
            _owner = owner;

            File.AppendAllText(logPath, "SettingsViewModel: Loading settings...\n");
            Console.WriteLine("SettingsViewModel: Loading settings...");
            // Load current settings
            _originalSettings = _settingsManager.GetSettings();
            
            // Ensure settings has non-null values
            if (_originalSettings == null)
            {
                File.AppendAllText(logPath, "ERROR: GetSettings returned null!\n");
                throw new InvalidOperationException("Settings manager returned null settings");
            }
            
            File.AppendAllText(logPath, $"SettingsViewModel: Settings loaded - MediaDirectory={_originalSettings.MediaDirectory ?? "NULL"}\n");
            _workingSettings = CloneSettings(_originalSettings);
            File.AppendAllText(logPath, "SettingsViewModel: Settings cloned successfully\n");
            
            File.AppendAllText(logPath, "SettingsViewModel: Clearing collections...\n");
            Console.WriteLine("SettingsViewModel: Collections already initialized inline");
            // Collections are already initialized inline, just clear them to be safe
            AudioDevices.Clear();
            KeyboardShortcuts.Clear();

            File.AppendAllText(logPath, "SettingsViewModel: Commands auto-generated...\n");
            Console.WriteLine("SettingsViewModel: Commands auto-generated...");

            File.AppendAllText(logPath, "SettingsViewModel: Setting up validation...\n");
            Console.WriteLine("SettingsViewModel: Setting up validation...");
            // Set up validation
            SetupValidation();
            
            File.AppendAllText(logPath, "SettingsViewModel: Loading audio devices...\n");
            Console.WriteLine("SettingsViewModel: Loading audio devices...");
            // Load data (after commands are set up)
            LoadAudioDevices();
            
            File.AppendAllText(logPath, "SettingsViewModel: Loading keyboard shortcuts...\n");
            Console.WriteLine("SettingsViewModel: Loading keyboard shortcuts...");
            LoadKeyboardShortcuts();
            
            File.AppendAllText(logPath, "SettingsViewModel: Loading from settings...\n");
            Console.WriteLine("SettingsViewModel: Loading from settings...");
            LoadFromSettings(_workingSettings);
            
            File.AppendAllText(logPath, "SettingsViewModel: Initialization complete!\n");
            Console.WriteLine("SettingsViewModel: Initialization complete!");
        }
        catch (Exception ex)
        {
            // Try to log the error, but don't fail if logging itself fails
            try
            {
                File.AppendAllText(logPath, $"SettingsViewModel: EXCEPTION - {ex.Message}\n");
                File.AppendAllText(logPath, $"SettingsViewModel: Stack trace - {ex.StackTrace}\n");
            }
            catch
            {
                // Ignore logging errors in exception handler - prevent cascading failures
            }

            Console.WriteLine($"SettingsViewModel: ERROR during initialization: {ex.Message}");
            Console.WriteLine($"SettingsViewModel: Stack trace: {ex.StackTrace}");
            throw; // Re-throw to let the caller handle it
        }
    }

    #region Properties

    // Observable collections (not auto-generated)
    public ObservableCollection<string> AudioDevices { get; } = new ObservableCollection<string>();
    public ObservableCollection<KeyboardShortcutItem> KeyboardShortcuts { get; } = new ObservableCollection<KeyboardShortcutItem>();

    // Computed property
    public bool HasValidationErrors =>
        !string.IsNullOrEmpty(MediaDirectoryError) ||
        !string.IsNullOrEmpty(CrossfadeDurationError) ||
        !string.IsNullOrEmpty(FontSizeError) ||
        !string.IsNullOrEmpty(PreloadBufferSizeError) ||
        !string.IsNullOrEmpty(CacheSizeError);

    // Partial methods for property change notifications
    // Note: Volume clamping removed - should be handled by UI validation

    // Validation error change handlers - notify HasValidationErrors
    partial void OnMediaDirectoryErrorChanged(string? value) => OnPropertyChanged(nameof(HasValidationErrors));
    partial void OnCrossfadeDurationErrorChanged(string? value) => OnPropertyChanged(nameof(HasValidationErrors));
    partial void OnFontSizeErrorChanged(string? value) => OnPropertyChanged(nameof(HasValidationErrors));
    partial void OnPreloadBufferSizeErrorChanged(string? value) => OnPropertyChanged(nameof(HasValidationErrors));
    partial void OnCacheSizeErrorChanged(string? value) => OnPropertyChanged(nameof(HasValidationErrors));

    #endregion

    #region Commands
    // Commands are auto-generated via RelayCommand attributes
    #endregion

    #region Command Implementations

    [RelayCommand]
    private async Task BrowseMediaDirectory()
    {
        if (_owner == null) return;

        var storageProvider = _owner.StorageProvider;
        if (storageProvider == null) return;

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Media Directory",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            MediaDirectory = result[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void TestAudio()
    {
        if (string.IsNullOrEmpty(SelectedAudioDevice))
            return;

        // TODO: Implement test audio functionality
        // This would play a short test tone through the selected audio device
        Console.WriteLine($"Testing audio on device: {SelectedAudioDevice}");
    }

    [RelayCommand]
    private void ResetShortcut(KeyboardShortcutItem item)
    {
        if (item != null)
        {
            item.Shortcut = item.DefaultShortcut;
        }
    }

    [RelayCommand]
    private async Task ResetToDefaults()
    {
        // Use SettingsManager to get defaults
        await _settingsManager.ResetToDefaultsAsync();
        var defaults = _settingsManager.GetSettings();
        
        // Update working copy
        _workingSettings = CloneSettings(defaults);
        LoadFromSettings(_workingSettings);
        
        // Reset keyboard shortcuts to defaults
        foreach (var shortcut in KeyboardShortcuts)
        {
            shortcut.Shortcut = shortcut.DefaultShortcut;
        }
        
        // Clear validation errors
        ClearValidationErrors();
    }

    [RelayCommand]
    private async Task Ok()
    {
        await Apply();
        CloseWindow();
    }

    [RelayCommand]
    private void Cancel()
    {
        // Revert to original settings (discard working copy)
        _workingSettings = CloneSettings(_originalSettings);
        
        // Clear any validation errors
        ClearValidationErrors();
        
        CloseWindow();
    }

    [RelayCommand]
    private async Task Apply()
    {
        try
        {
            // Validate all settings before applying
            ValidateMediaDirectory();
            ValidateCrossfadeDuration();
            ValidateFontSize();
            ValidatePreloadBufferSize();
            ValidateCacheSize();

            if (HasValidationErrors)
            {
                Console.WriteLine("Cannot apply settings: validation errors exist");
                return;
            }

            // Save current UI values to working settings
            SaveToSettings(_workingSettings);

            // Validate and save
            await _settingsManager.UpdateSettingsAsync(_workingSettings);

            // Update original settings to match
            _originalSettings = CloneSettings(_workingSettings);
            
            Console.WriteLine("Settings applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying settings: {ex.Message}");
            // TODO: Show error dialog to user
        }
    }

    #endregion

    #region Helper Methods

    private void SetupValidation()
    {
        // Validation is now handled via partial OnChanged methods
        // No subscriptions needed - validation errors automatically notify HasValidationErrors
    }

    private void ClearValidationErrors()
    {
        MediaDirectoryError = null;
        CrossfadeDurationError = null;
        FontSizeError = null;
        PreloadBufferSizeError = null;
        CacheSizeError = null;
    }

    private void LoadKeyboardShortcuts()
    {
        // TODO: Load from settings when keyboard shortcuts are added to AppSettings
        // For now, use default shortcuts
        var defaultShortcuts = new[]
        {
            ("Play/Pause", "Space"),
            ("Next Track", "Right"),
            ("Previous Track", "Left"),
            ("Volume Up", "Up"),
            ("Volume Down", "Down"),
            ("Mute/Unmute", "M"),
            ("Toggle Fullscreen", "F11"),
            ("Add to Playlist (End)", "Ctrl+A"),
            ("Add to Playlist (Next)", "Ctrl+Shift+A"),
            ("Remove from Playlist", "Delete"),
            ("Clear Playlist", "Ctrl+L"),
            ("Shuffle Playlist", "Ctrl+S"),
            ("Focus Search", "Ctrl+F"),
            ("Open Playlist Composer", "Ctrl+P"),
            ("Open Settings", "Ctrl+,"),
            ("Refresh Library", "Ctrl+R"),
            ("Toggle Display Mode", "Ctrl+D"),
            ("Exit Fullscreen/Close Dialog", "Escape")
        };

        KeyboardShortcuts.Clear();
        foreach (var (action, shortcut) in defaultShortcuts)
        {
            KeyboardShortcuts.Add(new KeyboardShortcutItem
            {
                Action = action,
                Shortcut = shortcut,
                DefaultShortcut = shortcut
            });
        }
    }

    private void LoadAudioDevices()
    {
        try
        {
            Console.WriteLine("LoadAudioDevices: Starting...");
            AudioDevices.Clear();
            
            if (_mediaPlayerController != null)
            {
                Console.WriteLine("LoadAudioDevices: Getting devices from controller...");
                var devices = _mediaPlayerController.GetAudioDevices();
                Console.WriteLine($"LoadAudioDevices: Found {devices?.Count ?? 0} devices");
                
                if (devices != null && devices.Count > 0)
                {
                    foreach (var device in devices)
                    {
                        if (device != null && !string.IsNullOrEmpty(device.Name))
                        {
                            AudioDevices.Add(device.Name);
                        }
                    }
                }

                // Set selected device from settings
                var currentDevice = _workingSettings?.AudioOutputDevice;
                if (!string.IsNullOrEmpty(currentDevice) && AudioDevices.Contains(currentDevice))
                {
                    SelectedAudioDevice = currentDevice;
                    Console.WriteLine($"LoadAudioDevices: Selected device from settings: {currentDevice}");
                }
                else if (AudioDevices.Count > 0)
                {
                    SelectedAudioDevice = AudioDevices[0];
                    Console.WriteLine($"LoadAudioDevices: Selected first device: {AudioDevices[0]}");
                }
            }
            
            // Always ensure we have at least one device
            if (AudioDevices.Count == 0)
            {
                Console.WriteLine("LoadAudioDevices: No devices found, adding default");
                AudioDevices.Add("Default Audio Device");
                SelectedAudioDevice = "Default Audio Device";
            }
            
            Console.WriteLine($"LoadAudioDevices: Complete. Total devices: {AudioDevices.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadAudioDevices: ERROR - {ex.Message}");
            Console.WriteLine($"LoadAudioDevices: Stack trace - {ex.StackTrace}");
            // Fallback
            AudioDevices.Clear();
            AudioDevices.Add("Default Audio Device");
            SelectedAudioDevice = "Default Audio Device";
        }
    }

    private void ValidateMediaDirectory()
    {
        if (string.IsNullOrWhiteSpace(MediaDirectory))
        {
            MediaDirectoryError = "Media directory cannot be empty";
        }
        else
        {
            MediaDirectoryError = null;
        }
    }

    private void ValidateCrossfadeDuration()
    {
        if (!_settingsManager.ValidateSetting(nameof(AppSettings.CrossfadeDuration), CrossfadeDuration, out var error))
        {
            CrossfadeDurationError = error;
        }
        else
        {
            CrossfadeDurationError = null;
        }
    }

    private void ValidateFontSize()
    {
        if (!_settingsManager.ValidateSetting(nameof(AppSettings.FontSize), FontSize, out var error))
        {
            FontSizeError = error;
        }
        else
        {
            FontSizeError = null;
        }
    }

    private void ValidatePreloadBufferSize()
    {
        if (!_settingsManager.ValidateSetting(nameof(AppSettings.PreloadBufferSize), PreloadBufferSize, out var error))
        {
            PreloadBufferSizeError = error;
        }
        else
        {
            PreloadBufferSizeError = null;
        }
    }

    private void ValidateCacheSize()
    {
        if (!_settingsManager.ValidateSetting(nameof(AppSettings.CacheSize), CacheSize, out var error))
        {
            CacheSizeError = error;
        }
        else
        {
            CacheSizeError = null;
        }
    }

    private void LoadFromSettings(AppSettings settings)
    {
        try
        {
            Console.WriteLine("LoadFromSettings: Starting...");
            
            // General
            MediaDirectory = settings.MediaDirectory ?? string.Empty;
            Console.WriteLine($"LoadFromSettings: MediaDirectory = {MediaDirectory}");
            
            DisplayModeIndex = settings.DisplayMode == DisplayMode.Single ? 0 : 1;
            AutoPlayEnabled = settings.AutoPlayEnabled;
            ShuffleMode = settings.ShuffleMode;

            // Audio
            VolumePercent = settings.Volume * 100.0;
            AudioBoostEnabled = settings.AudioBoostEnabled;
            CrossfadeEnabled = settings.CrossfadeEnabled;
            CrossfadeDuration = settings.CrossfadeDuration;
            
            Console.WriteLine($"LoadFromSettings: Audio settings loaded, checking device...");
            
            // Set audio device
            if (!string.IsNullOrEmpty(settings.AudioOutputDevice) && AudioDevices != null && AudioDevices.Contains(settings.AudioOutputDevice))
            {
                SelectedAudioDevice = settings.AudioOutputDevice;
                Console.WriteLine($"LoadFromSettings: Selected device from settings: {SelectedAudioDevice}");
            }
            else if (AudioDevices != null && AudioDevices.Count > 0)
            {
                SelectedAudioDevice = AudioDevices[0];
                Console.WriteLine($"LoadFromSettings: Selected first device: {SelectedAudioDevice}");
            }
            else
            {
                SelectedAudioDevice = "default";
                Console.WriteLine("LoadFromSettings: No devices available, using 'default'");
            }

            // Display
            ThemeIndex = settings.Theme?.Equals("dark", StringComparison.OrdinalIgnoreCase) == true ? 0 : 1;
            FontSize = settings.FontSize;
            VisualizationStyleIndex = (settings.VisualizationStyle ?? "bars").ToLowerInvariant() switch
            {
                "bars" => 0,
                "waveform" => 1,
                "circular" => 2,
                "particles" => 3,
                _ => 0
            };

            // Performance
            PreloadBufferSize = settings.PreloadBufferSize;
            CacheSize = settings.CacheSize;
            
            Console.WriteLine("LoadFromSettings: Complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in LoadFromSettings: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void SaveToSettings(AppSettings settings)
    {
        // General
        settings.MediaDirectory = MediaDirectory;
        settings.DisplayMode = DisplayModeIndex == 0 ? DisplayMode.Single : DisplayMode.Dual;
        settings.AutoPlayEnabled = AutoPlayEnabled;
        settings.ShuffleMode = ShuffleMode;

        // Audio
        settings.Volume = VolumePercent / 100.0;
        settings.AudioBoostEnabled = AudioBoostEnabled;
        settings.AudioOutputDevice = SelectedAudioDevice;
        settings.CrossfadeEnabled = CrossfadeEnabled;
        settings.CrossfadeDuration = CrossfadeDuration;

        // Display
        settings.Theme = ThemeIndex == 0 ? "dark" : "light";
        settings.FontSize = FontSize;
        settings.VisualizationStyle = VisualizationStyleIndex switch
        {
            0 => "bars",
            1 => "waveform",
            2 => "circular",
            3 => "particles",
            _ => "bars"
        };

        // Performance
        settings.PreloadBufferSize = PreloadBufferSize;
        settings.CacheSize = CacheSize;

        settings.LastModified = DateTime.UtcNow;
    }

    private AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            Id = source.Id ?? "default",
            MediaDirectory = source.MediaDirectory ?? string.Empty,
            DisplayMode = source.DisplayMode,
            Volume = source.Volume,
            AudioBoostEnabled = source.AudioBoostEnabled,
            AudioOutputDevice = source.AudioOutputDevice ?? "default",
            CrossfadeEnabled = source.CrossfadeEnabled,
            CrossfadeDuration = source.CrossfadeDuration,
            AutoPlayEnabled = source.AutoPlayEnabled,
            ShuffleMode = source.ShuffleMode,
            VisualizationStyle = source.VisualizationStyle ?? "bars",
            Theme = source.Theme ?? "dark",
            FontSize = source.FontSize,
            PreloadBufferSize = source.PreloadBufferSize,
            CacheSize = source.CacheSize,
            CreatedAt = source.CreatedAt,
            LastModified = source.LastModified
        };
    }

    private void CloseWindow()
    {
        _owner?.Close();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        // No disposables needed after ReactiveUI migration
        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Represents a keyboard shortcut configuration item
/// </summary>
public partial class KeyboardShortcutItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [ObservableProperty]
    private string _action = string.Empty;
    
    [ObservableProperty]
    private string _shortcut = string.Empty;
    
    [ObservableProperty]
    private string _defaultShortcut = string.Empty;
}
