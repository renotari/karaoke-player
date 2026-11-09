using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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
    private IDisposable? _youTubeSubscription;
    private YouTubeWindow? _youTubeWindow;

    public MainWindow()
    {
        InitializeComponent();
        
        // Video: hand the two native windows to the player controller once they exist
        foreach (var name in new[] { "VideoViewA", "VideoViewB" })
        {
            if (this.FindControl<EmbeddedVideoView>(name) is { } view)
                view.HandleCreated += (_, _) => AttachVideoWindows();
        }

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

        _youTubeSubscription = MessageBus.Current.Listen<OpenYouTubeMessage>()
            .Subscribe(message => Avalonia.Threading.Dispatcher.UIThread.Post(() => OpenYouTube(message.Query)));
    }

    /// <summary>
    /// Opens the Playlist Composer window
    /// </summary>
    public async Task OpenPlaylistComposerAsync()
    {
        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var app = App.Current;
                if (app?.MediaLibraryManager == null)
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.StatusMessage = "Media library not available";
                    }
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
                    viewModel.GetPlaylistManager()!,
                    () => viewModel.CurrentSong != null
                );

                var playlistComposer = new PlaylistComposerWindow
                {
                    DataContext = playlistComposerViewModel
                };
                // Needed for Close and for the M3U file pickers
                playlistComposerViewModel.SetWindow(playlistComposer);

                await playlistComposer.ShowDialog(this);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening playlist composer: {ex.Message}");
            if (DataContext is MainWindowViewModel vm)
            {
                vm.StatusMessage = $"Error opening playlist composer: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Opens the Settings window
    /// </summary>
    public async Task OpenSettingsAsync()
    {
        try
        {
            var app = App.Current;
            if (app?.SettingsManager == null)
            {
                ReportError("Settings not available", "See the log file for the startup error.");
                return;
            }

            // The dialog is the owner of its ViewModel: OK/Cancel close the dialog
            // (passing the main window here closed the whole application)
            var settingsWindow = new SettingsWindow();
            settingsWindow.DataContext = new SettingsViewModel(
                app.SettingsManager,
                app.MediaPlayerController,
                settingsWindow,
                app.YouTubeService);

            await settingsWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            ReportError("Error opening settings", ex.Message, ex);
        }
    }

    /// <summary>
    /// Opens the YouTube window (not modal: the app stays usable). When it is
    /// already open it is brought to the front, searching the new text if any.
    /// </summary>
    private void OpenYouTube(string? query)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm || App.Current?.YouTubeService is not { } service)
            {
                ReportError("YouTube not available", "See the log file for the startup error.");
                return;
            }

            if (_youTubeWindow != null)
            {
                if (!string.IsNullOrWhiteSpace(query) && _youTubeWindow.DataContext is YouTubeViewModel open)
                {
                    var fresh = new YouTubeViewModel(service, vm, query);
                    if (fresh.Query != open.Query)
                    {
                        open.Query = fresh.Query;
                        if (open.SearchCommand.CanExecute(null))
                            open.SearchCommand.Execute(null);
                    }
                }
                _youTubeWindow.Activate();
                return;
            }

            _youTubeWindow = new YouTubeWindow { DataContext = new YouTubeViewModel(service, vm, query) };
            _youTubeWindow.Closed += (_, _) => _youTubeWindow = null;
            _youTubeWindow.Show(this);
        }
        catch (Exception ex)
        {
            ReportError("Error opening YouTube window", ex.Message, ex);
        }
    }

    /// <summary>Logs a problem and shows it to the user (a WinExe has no console).</summary>
    private void ReportError(string title, string message, Exception? ex = null)
    {
        App.Current?.LoggingService?.LogError($"{title}: {message}", ex);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StatusMessage = $"{title}: {message}";
            vm.NotificationService?.ShowError(title, message);
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

        // In case the native video windows were created before the DataContext was set
        AttachVideoWindows();

        // Video Mode: collapse the side columns so the video takes the whole window
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsVideoMode))
                {
                    ApplyVideoModeLayout(vm.IsVideoMode);
                }
                else if (args.PropertyName == nameof(MainWindowViewModel.IsControlHandleExpanded) && vm.IsControlHandleExpanded)
                {
                    // Ready to type a song request
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        this.FindControl<TextBox>("QuickSearchTextBox")?.Focus());
                }
            };
            ApplyVideoModeLayout(vm.IsVideoMode);
        }
    }

    /// <summary>Gives the main window's video surfaces to the players (when both exist).</summary>
    public void AttachVideoWindows()
    {
        var a = this.FindControl<EmbeddedVideoView>("VideoViewA")?.Handle ?? IntPtr.Zero;
        var b = this.FindControl<EmbeddedVideoView>("VideoViewB")?.Handle ?? IntPtr.Zero;
        if (a != IntPtr.Zero && b != IntPtr.Zero)
        {
            (DataContext as MainWindowViewModel)?.AttachVideoWindows(a, b);
        }
    }

    /// <summary>Catalog and playlist column widths (the ones before Video Mode, if active).</summary>
    public (double Catalog, double Playlist) GetSideColumnWidths()
    {
        if (_savedColumnWidths != null)
            return (_savedColumnWidths[0].Value, _savedColumnWidths[3].Value);

        var grid = this.FindControl<Grid>("MainContentGrid");
        if (grid == null || grid.ColumnDefinitions.Count != 5)
            return (300, 300);
        return (grid.ColumnDefinitions[0].ActualWidth, grid.ColumnDefinitions[4].ActualWidth);
    }

    public void SetSideColumnWidths(double catalog, double playlist)
    {
        var grid = this.FindControl<Grid>("MainContentGrid");
        if (grid == null || grid.ColumnDefinitions.Count != 5 || _savedColumnWidths != null)
            return;
        if (catalog >= 150) grid.ColumnDefinitions[0].Width = new GridLength(catalog);
        if (playlist >= 150) grid.ColumnDefinitions[4].Width = new GridLength(playlist);
    }

    // Column widths saved before entering Video Mode (keeps sizes set with the splitters)
    private GridLength[]? _savedColumnWidths;

    private void ApplyVideoModeLayout(bool isVideoMode)
    {
        var grid = this.FindControl<Grid>("MainContentGrid");
        if (grid == null || grid.ColumnDefinitions.Count != 5)
            return;

        int[] sideColumns = { 0, 1, 3, 4 };

        if (isVideoMode)
        {
            _savedColumnWidths ??= Array.ConvertAll(sideColumns, i => grid.ColumnDefinitions[i].Width);
            foreach (var i in sideColumns)
                grid.ColumnDefinitions[i].Width = new GridLength(0);
        }
        else if (_savedColumnWidths != null)
        {
            for (int k = 0; k < sideColumns.Length; k++)
                grid.ColumnDefinitions[sideColumns[k]].Width = _savedColumnWidths[k];
            _savedColumnWidths = null;
        }
    }

    /// <summary>
    /// Double-click on a playlist song plays it right away.
    /// </summary>
    private void OnPlaylistDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedPlaylistItem != null)
        {
            vm.PlaySongNowCommand.Execute(vm.SelectedPlaylistItem);
        }
    }

    /// <summary>
    /// Double-click on a catalog song adds it to the playlist as "next" (the default queue position).
    /// </summary>
    private void OnCatalogDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedMediaFile != null)
        {
            vm.AddToPlaylistNextCommand.Execute(vm.SelectedMediaFile);
        }
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
            viewModel.ExpandControlHandleCommand.Execute(null);
        }
    }

    private void OnControlHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExpandControlHandleCommand.Execute(null);
        }
    }

    /// <summary>
    /// Opens a folder picker to select media directory
    /// </summary>
    public async Task OpenMediaDirectoryAsync()
    {
        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Media Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    var result = folders[0].Path.LocalPath;

                    // Saving the setting is enough: the app re-scans the new folder
                    // and the library updates through its events
                    var app = App.Current;
                    if (app?.SettingsManager == null)
                    {
                        ReportError("Cannot open the folder", "Settings not available: see the log file for the startup error.");
                    }
                    else
                    {
                        app.ForgetActiveMediaDirectory();
                        app.SettingsManager.SetSetting(nameof(Models.AppSettings.MediaDirectory), result);
                        await app.SettingsManager.SaveSettingsAsync();

                        if (DataContext is MainWindowViewModel viewModel)
                        {
                            viewModel.StatusMessage = $"Scanning {result}...";
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            ReportError("Error opening media directory", ex.Message, ex);
        }
    }

    /// <summary>
    /// Shows the About dialog
    /// </summary>
    public async Task ShowAboutDialogAsync()
    {
        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
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
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing about dialog: {ex.Message}");
            if (DataContext is MainWindowViewModel vm)
            {
                vm.StatusMessage = $"Error showing about dialog: {ex.Message}";
            }
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Cleanup subscriptions
        _playlistComposerSubscription?.Dispose();
        _settingsSubscription?.Dispose();
        _openMediaDirectorySubscription?.Dispose();
        _showAboutSubscription?.Dispose();
        _exitApplicationSubscription?.Dispose();
        _youTubeSubscription?.Dispose();
    }
}