using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using ReactiveUI;

namespace KaraokePlayer.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsManager _settingsManager;
    private readonly Window? _owner;
    private AppSettings _originalSettings;
    private AppSettings _workingSettings;

    // General Tab
    private string _mediaDirectory = string.Empty;
    private int _displayModeIndex;
    private bool _autoPlayEnabled;
    private bool _shuffleMode;

    // Audio Tab
    private double _volumePercent;
    private bool _audioBoostEnabled;
    private string _selectedAudioDevice = "default";
    private bool _crossfadeEnabled;
    private int _crossfadeDuration;

    // Display Tab
    private int _themeIndex;
    private int _fontSize;
    private int _visualizationStyleIndex;

    // Performance Tab
    private int _preloadBufferSize;
    private int _cacheSize;

    public SettingsViewModel() : this(null, null)
    {
        // Design-time constructor
    }

    public SettingsViewModel(ISettingsManager? settingsManager, Window? owner)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _owner = owner;

        // Load current settings
        _originalSettings = _settingsManager.GetSettings();
        _workingSettings = CloneSettings(_originalSettings);
        LoadFromSettings(_workingSettings);

        // Initialize audio devices
        AudioDevices = new ObservableCollection<string>
        {
            "Default Audio Device",
            "Speakers",
            "Headphones",
            "HDMI Audio"
        };
        SelectedAudioDevice = AudioDevices.FirstOrDefault() ?? "Default Audio Device";

        // Initialize keyboard shortcuts
        KeyboardShortcuts = new ObservableCollection<KeyboardShortcutItem>
        {
            new KeyboardShortcutItem { Action = "Play/Pause", Shortcut = "Space", DefaultShortcut = "Space" },
            new KeyboardShortcutItem { Action = "Next Track", Shortcut = "Right", DefaultShortcut = "Right" },
            new KeyboardShortcutItem { Action = "Previous Track", Shortcut = "Left", DefaultShortcut = "Left" },
            new KeyboardShortcutItem { Action = "Volume Up", Shortcut = "Up", DefaultShortcut = "Up" },
            new KeyboardShortcutItem { Action = "Volume Down", Shortcut = "Down", DefaultShortcut = "Down" },
            new KeyboardShortcutItem { Action = "Mute/Unmute", Shortcut = "M", DefaultShortcut = "M" },
            new KeyboardShortcutItem { Action = "Toggle Fullscreen", Shortcut = "F11", DefaultShortcut = "F11" },
            new KeyboardShortcutItem { Action = "Add to Playlist (End)", Shortcut = "Ctrl+A", DefaultShortcut = "Ctrl+A" },
            new KeyboardShortcutItem { Action = "Add to Playlist (Next)", Shortcut = "Ctrl+Shift+A", DefaultShortcut = "Ctrl+Shift+A" },
            new KeyboardShortcutItem { Action = "Remove from Playlist", Shortcut = "Delete", DefaultShortcut = "Delete" },
            new KeyboardShortcutItem { Action = "Clear Playlist", Shortcut = "Ctrl+L", DefaultShortcut = "Ctrl+L" },
            new KeyboardShortcutItem { Action = "Shuffle Playlist", Shortcut = "Ctrl+S", DefaultShortcut = "Ctrl+S" },
            new KeyboardShortcutItem { Action = "Focus Search", Shortcut = "Ctrl+F", DefaultShortcut = "Ctrl+F" },
            new KeyboardShortcutItem { Action = "Open Playlist Composer", Shortcut = "Ctrl+P", DefaultShortcut = "Ctrl+P" },
            new KeyboardShortcutItem { Action = "Open Settings", Shortcut = "Ctrl+,", DefaultShortcut = "Ctrl+," },
            new KeyboardShortcutItem { Action = "Refresh Library", Shortcut = "Ctrl+R", DefaultShortcut = "Ctrl+R" },
            new KeyboardShortcutItem { Action = "Toggle Display Mode", Shortcut = "Ctrl+D", DefaultShortcut = "Ctrl+D" },
            new KeyboardShortcutItem { Action = "Exit Fullscreen/Close Dialog", Shortcut = "Escape", DefaultShortcut = "Escape" }
        };

        // Commands
        BrowseMediaDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseMediaDirectoryAsync);
        TestAudioCommand = ReactiveCommand.Create(TestAudio);
        ResetShortcutCommand = ReactiveCommand.Create<KeyboardShortcutItem>(ResetShortcut);
        ResetToDefaultsCommand = ReactiveCommand.CreateFromTask(ResetToDefaultsAsync);
        OkCommand = ReactiveCommand.CreateFromTask(OkAsync);
        CancelCommand = ReactiveCommand.Create(Cancel);
        ApplyCommand = ReactiveCommand.CreateFromTask(ApplyAsync);
    }

    #region Properties

    // General Tab
    public string MediaDirectory
    {
        get => _mediaDirectory;
        set => this.RaiseAndSetIfChanged(ref _mediaDirectory, value);
    }

    public int DisplayModeIndex
    {
        get => _displayModeIndex;
        set => this.RaiseAndSetIfChanged(ref _displayModeIndex, value);
    }

    public bool AutoPlayEnabled
    {
        get => _autoPlayEnabled;
        set => this.RaiseAndSetIfChanged(ref _autoPlayEnabled, value);
    }

    public bool ShuffleMode
    {
        get => _shuffleMode;
        set => this.RaiseAndSetIfChanged(ref _shuffleMode, value);
    }

    // Audio Tab
    public double VolumePercent
    {
        get => _volumePercent;
        set => this.RaiseAndSetIfChanged(ref _volumePercent, value);
    }

    public bool AudioBoostEnabled
    {
        get => _audioBoostEnabled;
        set => this.RaiseAndSetIfChanged(ref _audioBoostEnabled, value);
    }

    public ObservableCollection<string> AudioDevices { get; }

    public string SelectedAudioDevice
    {
        get => _selectedAudioDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedAudioDevice, value);
    }

    public bool CrossfadeEnabled
    {
        get => _crossfadeEnabled;
        set => this.RaiseAndSetIfChanged(ref _crossfadeEnabled, value);
    }

    public int CrossfadeDuration
    {
        get => _crossfadeDuration;
        set => this.RaiseAndSetIfChanged(ref _crossfadeDuration, value);
    }

    // Display Tab
    public int ThemeIndex
    {
        get => _themeIndex;
        set => this.RaiseAndSetIfChanged(ref _themeIndex, value);
    }

    public int FontSize
    {
        get => _fontSize;
        set => this.RaiseAndSetIfChanged(ref _fontSize, value);
    }

    public int VisualizationStyleIndex
    {
        get => _visualizationStyleIndex;
        set => this.RaiseAndSetIfChanged(ref _visualizationStyleIndex, value);
    }

    // Keyboard Tab
    public ObservableCollection<KeyboardShortcutItem> KeyboardShortcuts { get; }

    // Performance Tab
    public int PreloadBufferSize
    {
        get => _preloadBufferSize;
        set => this.RaiseAndSetIfChanged(ref _preloadBufferSize, value);
    }

    public int CacheSize
    {
        get => _cacheSize;
        set => this.RaiseAndSetIfChanged(ref _cacheSize, value);
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> BrowseMediaDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> TestAudioCommand { get; }
    public ReactiveCommand<KeyboardShortcutItem, Unit> ResetShortcutCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }

    #endregion

    #region Command Implementations

    private async Task BrowseMediaDirectoryAsync()
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

    private void TestAudio()
    {
        // TODO: Implement test audio functionality
        // This would play a short test tone through the selected audio device
        Console.WriteLine($"Testing audio on device: {SelectedAudioDevice}");
    }

    private void ResetShortcut(KeyboardShortcutItem item)
    {
        if (item != null)
        {
            item.Shortcut = item.DefaultShortcut;
        }
    }

    private async Task ResetToDefaultsAsync()
    {
        // Create default settings
        var defaults = CreateDefaultSettings();
        LoadFromSettings(defaults);
        
        // Reset keyboard shortcuts
        foreach (var shortcut in KeyboardShortcuts)
        {
            shortcut.Shortcut = shortcut.DefaultShortcut;
        }

        await Task.CompletedTask;
    }

    private async Task OkAsync()
    {
        await ApplyAsync();
        CloseWindow();
    }

    private void Cancel()
    {
        CloseWindow();
    }

    private async Task ApplyAsync()
    {
        try
        {
            // Save current UI values to working settings
            SaveToSettings(_workingSettings);

            // Validate and save
            await _settingsManager.UpdateSettingsAsync(_workingSettings);

            // Update original settings to match
            _originalSettings = CloneSettings(_workingSettings);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying settings: {ex.Message}");
            // TODO: Show error dialog to user
        }
    }

    #endregion

    #region Helper Methods

    private void LoadFromSettings(AppSettings settings)
    {
        // General
        MediaDirectory = settings.MediaDirectory;
        DisplayModeIndex = settings.DisplayMode == DisplayMode.Single ? 0 : 1;
        AutoPlayEnabled = settings.AutoPlayEnabled;
        ShuffleMode = settings.ShuffleMode;

        // Audio
        VolumePercent = settings.Volume * 100.0;
        AudioBoostEnabled = settings.AudioBoostEnabled;
        CrossfadeEnabled = settings.CrossfadeEnabled;
        CrossfadeDuration = settings.CrossfadeDuration;

        // Display
        ThemeIndex = settings.Theme.Equals("dark", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        FontSize = settings.FontSize;
        VisualizationStyleIndex = settings.VisualizationStyle.ToLowerInvariant() switch
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
            Id = source.Id,
            MediaDirectory = source.MediaDirectory,
            DisplayMode = source.DisplayMode,
            Volume = source.Volume,
            AudioBoostEnabled = source.AudioBoostEnabled,
            AudioOutputDevice = source.AudioOutputDevice,
            CrossfadeEnabled = source.CrossfadeEnabled,
            CrossfadeDuration = source.CrossfadeDuration,
            AutoPlayEnabled = source.AutoPlayEnabled,
            ShuffleMode = source.ShuffleMode,
            VisualizationStyle = source.VisualizationStyle,
            Theme = source.Theme,
            FontSize = source.FontSize,
            PreloadBufferSize = source.PreloadBufferSize,
            CacheSize = source.CacheSize,
            CreatedAt = source.CreatedAt,
            LastModified = source.LastModified
        };
    }

    private AppSettings CreateDefaultSettings()
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

    private void CloseWindow()
    {
        _owner?.Close();
    }

    #endregion
}

/// <summary>
/// Represents a keyboard shortcut configuration item
/// </summary>
public class KeyboardShortcutItem : ReactiveObject
{
    private string _action = string.Empty;
    private string _shortcut = string.Empty;
    private string _defaultShortcut = string.Empty;

    public string Action
    {
        get => _action;
        set => this.RaiseAndSetIfChanged(ref _action, value);
    }

    public string Shortcut
    {
        get => _shortcut;
        set => this.RaiseAndSetIfChanged(ref _shortcut, value);
    }

    public string DefaultShortcut
    {
        get => _defaultShortcut;
        set => this.RaiseAndSetIfChanged(ref _defaultShortcut, value);
    }
}
