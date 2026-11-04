using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace KaraokePlayer.Views;

/// <summary>
/// Native child window that LibVLC renders video into.
///
/// Instead of binding one MediaPlayer (like LibVLCSharp's VideoView), it exposes its
/// native handle so the MediaPlayerController can attach both of its players to it:
/// that is what makes crossfades between two videos possible without LibVLC opening
/// a separate "VLC media player" window for the second player.
///
/// Like any native control, nothing can be drawn on top of it by Avalonia.
/// </summary>
public class EmbeddedVideoView : NativeControlHost
{
    /// <summary>Native handle (HWND / X11 window), or IntPtr.Zero when not created yet.</summary>
    public IntPtr Handle { get; private set; }

    /// <summary>Raised when the native window has been created.</summary>
    public event EventHandler<IntPtr>? HandleCreated;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        Handle = handle.Handle;
        HandleCreated?.Invoke(this, Handle);
        return handle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        Handle = IntPtr.Zero;
        base.DestroyNativeControlCore(control);
    }
}
