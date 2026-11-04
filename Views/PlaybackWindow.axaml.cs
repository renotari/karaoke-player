using System;
using Avalonia.Controls;
using Avalonia.Input;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

/// <summary>
/// Dual screen playback window (usually fullscreen on the second monitor).
/// Its DataContext is the MainWindowViewModel; while it is open the players
/// render into its video surfaces.
/// </summary>
public partial class PlaybackWindow : Window
{
    public PlaybackWindow()
    {
        InitializeComponent();

        foreach (var name in new[] { "VideoViewA", "VideoViewB" })
        {
            if (this.FindControl<EmbeddedVideoView>(name) is { } view)
                view.HandleCreated += (_, _) => AttachVideoWindows();
        }

        // Double-click toggles fullscreen, Escape leaves it
        DoubleTapped += (_, _) => ToggleFullscreen();
        KeyDown += OnKeyDown;
    }

    /// <summary>Gives this window's surfaces to the players (when both exist).</summary>
    public void AttachVideoWindows()
    {
        var a = this.FindControl<EmbeddedVideoView>("VideoViewA")?.Handle ?? IntPtr.Zero;
        var b = this.FindControl<EmbeddedVideoView>("VideoViewB")?.Handle ?? IntPtr.Zero;
        if (a != IntPtr.Zero && b != IntPtr.Zero)
        {
            (DataContext as MainWindowViewModel)?.AttachVideoWindows(a, b);
        }
    }

    private void ToggleFullscreen()
    {
        if (DataContext is MainWindowViewModel vm)
            vm.IsFullscreen = !vm.IsFullscreen;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (e.Key == Key.Escape && vm.IsFullscreen)
        {
            vm.IsFullscreen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }
}
