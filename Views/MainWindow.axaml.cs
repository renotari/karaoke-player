using Avalonia.Controls;
using Avalonia.Input;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Add keyboard event handler for shortcuts
        KeyDown += OnKeyDown;
        
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
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        // Handle keyboard shortcuts
        switch (e.Key)
        {
            case Key.Space:
                if (!e.Handled)
                {
                    viewModel.PlayPauseCommand.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                }
                break;

            case Key.Right:
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    viewModel.NextCommand.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                }
                break;

            case Key.Left:
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    viewModel.PreviousCommand.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                }
                break;

            case Key.V:
                if (e.KeyModifiers == KeyModifiers.Control)
                {
                    viewModel.ToggleVideoModeCommand.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                if (viewModel.IsVideoMode)
                {
                    if (viewModel.IsControlHandleExpanded)
                    {
                        viewModel.CollapseControlHandleCommand.Execute(System.Reactive.Unit.Default);
                    }
                    else
                    {
                        viewModel.ToggleVideoModeCommand.Execute(System.Reactive.Unit.Default);
                    }
                    e.Handled = true;
                }
                break;
        }
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