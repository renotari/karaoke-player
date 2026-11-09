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

    // YouTube Tab
    /// <summary>0 = 480p, 1 = 720p, 2 = 1080p</summary>
    [ObservableProperty]
    private int _youTubeQualityIndex = 1;

    [ObservableProperty]
    private string _youTubeToolsStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateYouTubeToolsCommand))]
    private bool _isUpdatingYouTubeTools;

    private readonly IYouTubeService? _youTubeService;

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

    public SettingsViewModel(ISettingsManager? settingsManager, IMediaPlayerController? mediaPlayerController, Window? owner,
        IYouTubeService? youTubeService = null)
    {
        _youTubeService = youTubeService;
        RefreshYouTubeToolsStatus();
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _mediaPlayerController = mediaPlayerController;
        _owner = owner;

        _originalSettings = _settingsManager.GetSettings()
            ?? throw new InvalidOperationException("Settings manager returned null settings");
        _workingSettings = CloneSettings(_originalSettings);

        LoadAudioDevices();
        LoadKeyboardShortcuts(_workingSettings);
        LoadFromSettings(_workingSettings);
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

    private void RefreshYouTubeToolsStatus()
    {
        if (_youTubeService == null)
        {
            YouTubeToolsStatus = "YouTube downloads are not available";
            return;
        }

        var tools = _youTubeService.GetToolsStatus();
        static string Line(string name, string? path, string missing) => $"{name}: {path ?? missing}";
        YouTubeToolsStatus = string.Join("\n",
            Line("yt-dlp", tools.YtDlp, "not installed (downloaded automatically on first use)"),
            Line("ffmpeg", tools.Ffmpeg, "not installed (videos limited to 360p)"),
            Line("deno", tools.Deno, "not installed (some videos may not download)"));
    }

    private bool CanUpdateYouTubeTools() => _youTubeService != null && !IsUpdatingYouTubeTools;

    /// <summary>Downloads the latest yt-dlp (and ffmpeg/deno on Windows): YouTube changes often.</summary>
    [RelayCommand(CanExecute = nameof(CanUpdateYouTubeTools))]
    private async Task UpdateYouTubeTools()
    {
        if (_youTubeService == null)
            return;

        IsUpdatingYouTubeTools = true;
        try
        {
            var status = new Progress<string>(text => YouTubeToolsStatus = text);
            await _youTubeService.EnsureToolsAsync(update: true, status);
            RefreshYouTubeToolsStatus();
        }
        catch (Exception ex)
        {
            RefreshYouTubeToolsStatus();
            YouTubeToolsStatus = $"Update failed: {ex.Message}\n\n{YouTubeToolsStatus}";
        }
        finally
        {
            IsUpdatingYouTubeTools = false;
        }
    }

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
        if (_mediaPlayerController == null)
            return;

        // The dialog lists device names; the controller wants the device id
        var device = _mediaPlayerController.GetAudioDevices()
            .FirstOrDefault(d => d.Name == SelectedAudioDevice);
        _mediaPlayerController.PlayTestTone(device?.Id ?? string.Empty);
    }

    [RelayCommand]
    private void ResetShortcut(KeyboardShortcutItem item)
    {
        if (item != null)
        {
            item.Shortcut = item.DefaultShortcut;
        }
    }

    /// <summary>
    /// Puts default values in the dialog. Nothing is saved until OK/Apply
    /// (the media folder is kept: resetting it would re-scan the library).
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = new AppSettings
        {
            MediaDirectory = MediaDirectory,
            KeyboardShortcuts = AppSettings.GetDefaultKeyboardShortcuts()
        };

        _workingSettings = CloneSettings(defaults);
        LoadKeyboardShortcuts(_workingSettings);
        LoadFromSettings(_workingSettings);
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

    /// <summary>Shortcut action ids (as stored in AppSettings) and their labels.</summary>
    private static readonly (string Id, string Label)[] ShortcutActions =
    {
        ("PlayPause", "Play/Pause"),
        ("Stop", "Stop"),
        ("Next", "Next Track"),
        ("Previous", "Previous Track"),
        ("VolumeUp", "Volume Up"),
        ("VolumeDown", "Volume Down"),
        ("Mute", "Mute/Unmute"),
        ("ToggleFullscreen", "Toggle Fullscreen"),
        ("AddToPlaylistEnd", "Add to Playlist (End)"),
        ("AddToPlaylistNext", "Add to Playlist (Next)"),
        ("RemoveFromPlaylist", "Remove from Playlist"),
        ("ClearPlaylist", "Clear Playlist"),
        ("ShufflePlaylist", "Shuffle Playlist"),
        ("FocusSearch", "Focus Search"),
        ("OpenPlaylistComposer", "Open Playlist Composer"),
        ("OpenSettings", "Open Settings"),
        ("OpenYouTube", "Download from YouTube"),
        ("RefreshLibrary", "Refresh Library"),
        ("ToggleDisplayMode", "Toggle Display Mode"),
        ("CloseDialog", "Exit Fullscreen/Close Dialog"),
    };

    private void LoadKeyboardShortcuts(AppSettings settings)
    {
        var defaults = AppSettings.GetDefaultKeyboardShortcuts();
        var current = settings.KeyboardShortcuts ?? defaults;

        KeyboardShortcuts.Clear();
        foreach (var (id, label) in ShortcutActions)
        {
            defaults.TryGetValue(id, out var defaultShortcut);
            KeyboardShortcuts.Add(new KeyboardShortcutItem
            {
                Id = id,
                Action = label,
                Shortcut = current.TryGetValue(id, out var shortcut) ? shortcut : defaultShortcut ?? string.Empty,
                DefaultShortcut = defaultShortcut ?? string.Empty
            });
        }
    }

    private void LoadAudioDevices()
    {
        AudioDevices.Clear();

        try
        {
            var devices = _mediaPlayerController?.GetAudioDevices();
            if (devices != null)
            {
                foreach (var device in devices)
                {
                    if (!string.IsNullOrEmpty(device.Name) && !AudioDevices.Contains(device.Name))
                        AudioDevices.Add(device.Name);
                }
            }
        }
        catch (Exception)
        {
            // Device list is optional: keep at least the default entry
        }

        if (AudioDevices.Count == 0)
            AudioDevices.Add(MediaPlayerController.DefaultAudioDeviceName);
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
            
            // General
            MediaDirectory = settings.MediaDirectory ?? string.Empty;
            
            DisplayModeIndex = settings.DisplayMode == DisplayMode.Single ? 0 : 1;
            AutoPlayEnabled = settings.AutoPlayEnabled;
            ShuffleMode = settings.ShuffleMode;

            // Audio
            VolumePercent = settings.Volume * 100.0;
            AudioBoostEnabled = settings.AudioBoostEnabled;
            CrossfadeEnabled = settings.CrossfadeEnabled;
            CrossfadeDuration = settings.CrossfadeDuration;
            
            
            // Set audio device
            if (!string.IsNullOrEmpty(settings.AudioOutputDevice) && AudioDevices.Contains(settings.AudioOutputDevice))
            {
                SelectedAudioDevice = settings.AudioOutputDevice;
            }
            else if (AudioDevices.Count > 0)
            {
                SelectedAudioDevice = AudioDevices[0];
            }
            else
            {
                SelectedAudioDevice = "default";
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
                "none" => 4,
                _ => 0
            };

            // YouTube
            YouTubeQualityIndex = settings.YouTubeMaxHeight switch { <= 480 => 0, >= 1080 => 2, _ => 1 };

            // Performance
            PreloadBufferSize = settings.PreloadBufferSize;
            CacheSize = settings.CacheSize;
            
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
            4 => "none",
            _ => "bars"
        };

        // YouTube
        settings.YouTubeMaxHeight = YouTubeQualityIndex switch { 0 => 480, 2 => 1080, _ => 720 };

        // Performance
        settings.PreloadBufferSize = PreloadBufferSize;
        settings.CacheSize = CacheSize;

        // Keyboard
        settings.KeyboardShortcuts = KeyboardShortcuts
            .Where(k => !string.IsNullOrWhiteSpace(k.Shortcut))
            .ToDictionary(k => k.Id, k => k.Shortcut.Trim());

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
            KeyboardShortcuts = new System.Collections.Generic.Dictionary<string, string>(
                source.KeyboardShortcuts ?? AppSettings.GetDefaultKeyboardShortcuts()),
            // Not edited in the dialog, but must survive OK/Apply
            MainWindowBounds = source.MainWindowBounds,
            PlaybackWindowBounds = source.PlaybackWindowBounds,
            CatalogColumnWidth = source.CatalogColumnWidth,
            YouTubeMaxHeight = source.YouTubeMaxHeight,
            PlaylistColumnWidth = source.PlaylistColumnWidth,
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
    /// <summary>Action id as stored in AppSettings.KeyboardShortcuts (e.g. "PlayPause")</summary>
    public string Id { get; set; } = string.Empty;

    [ObservableProperty]
    private string _action = string.Empty;
    
    [ObservableProperty]
    private string _shortcut = string.Empty;
    
    [ObservableProperty]
    private string _defaultShortcut = string.Empty;
}
