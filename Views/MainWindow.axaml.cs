using Avalonia.Controls;
using Avalonia.Input;
using KaraokePlayer.ViewModels;
using KaraokePlayer.Services;
using System;
using System.Threading.Tasks;
using ReactiveUI;

namespace KaraokePlayer.Views;

public partial class MainWindow : Window
{
    private TextBox? _searchTextBox;
    private IDisposable? _playlistComposerSubscription;
    private IDisposable? _settingsSubscription;

    public MainWindow()
    {
        InitializeComponent();
        
        // Wire up pointer events for control handle after initialization
        Opened += OnWindowOpened;
        Closed += OnWindowClosed;
        
        // Subscribe to window opening messages
        _playlistComposerSubscription = MessageBus.Current.Listen<OpenPlaylistComposerMessage>()
            .Subscribe(async _ => await OpenPlaylistComposerAsync());
            
        _settingsSubscription = MessageBus.Current.Listen<OpenSettingsMessage>()
            .Subscribe(async _ => await OpenSettingsAsync());
    }

    /// <summary>
    /// Opens the Playlist Composer window
    /// </summary>
    public async Task OpenPlaylistComposerAsync()
    {
        var playlistComposer = new PlaylistComposerWindow
        {
            DataContext = new PlaylistComposerViewModel()
        };
        await playlistComposer.ShowDialog(this);
    }

    /// <summary>
    /// Opens the Settings window
    /// </summary>
    public async Task OpenSettingsAsync()
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel()
        };
        await settingsWindow.ShowDialog(this);
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

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Cleanup subscriptions
        _playlistComposerSubscription?.Dispose();
        _settingsSubscription?.Dispose();
    }
}