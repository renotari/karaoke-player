using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using KaraokePlayer.ViewModels;
using KaraokePlayer.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using ReactiveUI;

namespace KaraokePlayer.Views;

public partial class MainWindow : Window
{
    private TextBox? _searchTextBox;
    private IDisposable? _playlistComposerSubscription;
    private IDisposable? _settingsSubscription;
    private IDisposable? _openMediaDirectorySubscription;
    private IDisposable? _showAboutSubscription;
    private IDisposable? _exitApplicationSubscription;

    public MainWindow()
    {
        InitializeComponent();
        
        // Wire up pointer events for control handle after initialization
        Opened += OnWindowOpened;
        Closed += OnWindowClosed;
        
        // Subscribe to window opening messages
        _playlistComposerSubscription = MessageBus.Current.Listen<OpenPlaylistComposerMessage>()
            .Subscribe(async _ => await OpenPlaylistComposerAsync());
            
        _settingsSubscription = MessageBus.Current.Listen<Services.OpenSettingsMessage>()
            .Subscribe(async _ => await OpenSettingsAsync());

        _openMediaDirectorySubscription = MessageBus.Current.Listen<OpenMediaDirectoryMessage>()
            .Subscribe(async _ => await OpenMediaDirectoryAsync());

        _showAboutSubscription = MessageBus.Current.Listen<ShowAboutMessage>()
            .Subscribe(async _ => await ShowAboutDialogAsync());

        _exitApplicationSubscription = MessageBus.Current.Listen<ExitApplicationMessage>()
            .Subscribe(_ => Close());
    }

    /// <summary>
    /// Opens the Playlist Composer window
    /// </summary>
    public async Task OpenPlaylistComposerAsync()
    {
        var app = App.Current;
        if (app?.MediaLibraryManager == null)
        {
            // Services not available, show error
            return;
        }

        // Get the playlist manager from the MainWindowViewModel
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel == null)
        {
            return;
        }

        var playlistComposerViewModel = new PlaylistComposerViewModel(
            app.MediaLibraryManager,
            viewModel.GetPlaylistManager()
        );

        var playlistComposer = new PlaylistComposerWindow
        {
            DataContext = playlistComposerViewModel
        };
        
        await playlistComposer.ShowDialog(this);
    }

    /// <summary>
    /// Opens the Settings window
    /// </summary>
    public async Task OpenSettingsAsync()
    {
        try
        {
            // Ensure we're on the UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var app = App.Current;
                
                // Log to file
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "KaraokePlayer", "Logs", "settings-debug.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"\n{DateTime.Now}: OpenSettingsAsync called\n");
                
                if (app?.SettingsManager == null)
                {
                    File.AppendAllText(logPath, "ERROR: SettingsManager is null\n");
                    Console.WriteLine("Error: SettingsManager is null");
                    return;
                }

                if (app?.MediaPlayerController == null)
                {
                    File.AppendAllText(logPath, "ERROR: MediaPlayerController is null\n");
                    Console.WriteLine("Error: MediaPlayerController is null");
                    return;
                }

                File.AppendAllText(logPath, "Creating SettingsViewModel...\n");
                Console.WriteLine("Creating SettingsViewModel...");
                
                SettingsViewModel settingsViewModel;
                try
                {
                    settingsViewModel = new SettingsViewModel(
                        app.SettingsManager,
                        app.MediaPlayerController,
                        this
                    );
                    File.AppendAllText(logPath, "SettingsViewModel created successfully\n");
                }
                catch (Exception vmEx)
                {
                    File.AppendAllText(logPath, $"ERROR creating SettingsViewModel: {vmEx.Message}\n");
                    File.AppendAllText(logPath, $"Stack trace: {vmEx.StackTrace}\n");
                    Console.WriteLine($"ERROR creating SettingsViewModel: {vmEx.Message}");
                    throw;
                }
                File.AppendAllText(logPath, "Creating SettingsWindow...\n");
                Console.WriteLine("Creating SettingsWindow...");
                
                var settingsWindow = new SettingsWindow
                {
                    DataContext = settingsViewModel
                };
                
                File.AppendAllText(logPath, "SettingsWindow created, showing dialog...\n");
                Console.WriteLine("Showing Settings dialog...");
                await settingsWindow.ShowDialog(this);
                
                File.AppendAllText(logPath, "Settings dialog closed normally\n");
                Console.WriteLine("Settings dialog closed");
            });
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KaraokePlayer", "Logs", "settings-debug.log");
            File.AppendAllText(logPath, $"EXCEPTION: {ex.Message}\n");
            File.AppendAllText(logPath, $"Stack trace: {ex.StackTrace}\n");
            
            Console.WriteLine($"Error opening settings: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Show error to user
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.StatusMessage = $"Error opening settings: {ex.Message}";
                }
            });
        }
    }

    private void OnWindowOpened(object? sender, System.EventArgs e)
    {
        // Wire up pointer events for the control handles
        var collapsedHandle = this.FindControl<Border>("CollapsedHandle");
        var expandedHandle = this.FindControl<Border>("ExpandedHandle");

        if (collapsedHandle != null)
        {
            collapsedHandle.PointerEntered += OnControlHandlePointerEnter;
            collapsedHandle.PointerPressed += OnControlHandlePointerPressed;
        }

        if (expandedHandle != null)
        {
            expandedHandle.PointerEntered += OnControlHandlePointerEnter;
        }

        // Find the search text box for focus management
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
    }

    /// <summary>
    /// Focus the search text box (called from keyboard shortcut)
    /// </summary>
    public void FocusSearchBox()
    {
        _searchTextBox?.Focus();
    }

    private void OnControlHandlePointerEnter(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExpandControlHandleCommand.Execute(System.Reactive.Unit.Default);
        }
    }

    private void OnControlHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExpandControlHandleCommand.Execute(System.Reactive.Unit.Default);
        }
    }

    /// <summary>
    /// Opens a folder picker to select media directory
    /// </summary>
    public async Task OpenMediaDirectoryAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Media Directory"
        };

        var result = await dialog.ShowAsync(this);
        
        if (!string.IsNullOrEmpty(result))
        {
            var app = App.Current;
            if (app?.MediaLibraryManager != null && app?.SettingsManager != null)
            {
                // Save the selected directory to settings
                app.SettingsManager.SetSetting(nameof(Models.AppSettings.MediaDirectory), result);
                await app.SettingsManager.SaveSettingsAsync();

                // Scan the new directory
                await app.MediaLibraryManager.ScanDirectoryAsync(result);
                app.MediaLibraryManager.StartMonitoring();

                // Update the ViewModel
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.StatusMessage = $"Scanning {result}...";
                    
                    // Reload media files
                    var files = await app.MediaLibraryManager.GetMediaFilesAsync();
                    viewModel.MediaFiles.Clear();
                    viewModel.FilteredMediaFiles.Clear();
                    
                    foreach (var file in files)
                    {
                        viewModel.MediaFiles.Add(file);
                        viewModel.FilteredMediaFiles.Add(file);
                    }
                    
                    viewModel.MediaLibraryCount = files.Count;
                    viewModel.StatusMessage = $"Loaded {files.Count} files from {result}";
                }
            }
        }
    }

    /// <summary>
    /// Shows the About dialog
    /// </summary>
    public async Task ShowAboutDialogAsync()
    {
        var aboutDialog = new Window
        {
            Title = "About Karaoke Player",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10
        };

        content.Children.Add(new TextBlock
        {
            Text = "Karaoke Player",
            FontSize = 24,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = "Version 1.0.0",
            FontSize = 14,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = "\nA professional karaoke player application with support for video and audio files, playlist management, and dual-screen display.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextAlignment = Avalonia.Media.TextAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = "\n© 2024 Karaoke Player",
            FontSize = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 20, 0, 0)
        });

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            Padding = new Avalonia.Thickness(30, 5, 30, 5)
        };
        closeButton.Click += (s, e) => aboutDialog.Close();
        content.Children.Add(closeButton);

        aboutDialog.Content = content;
        await aboutDialog.ShowDialog(this);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Cleanup subscriptions
        _playlistComposerSubscription?.Dispose();
        _settingsSubscription?.Dispose();
        _openMediaDirectorySubscription?.Dispose();
        _showAboutSubscription?.Dispose();
        _exitApplicationSubscription?.Dispose();
    }
}