using Avalonia.Controls;
using Avalonia.Input;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

public partial class MainWindow : Window
{
    private TextBox? _searchTextBox;

    public MainWindow()
    {
        InitializeComponent();
        
        // Wire up pointer events for control handle after initialization
        Opened += OnWindowOpened;
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
}