using Avalonia.Platform;
using LibVLCSharp.Avalonia;

namespace KaraokePlayer.Views;

/// <summary>
/// VideoView that also attaches its MediaPlayer when the native window is created.
///
/// LibVLCSharp's VideoView only passes the window handle to the MediaPlayer when the
/// MediaPlayer property is set or on Initialized. With a binding, the MediaPlayer is
/// usually set before the native window exists, so the handle is never passed and
/// LibVLC opens its own separate "VLC media player" window instead.
/// </summary>
public class EmbeddedVideoView : VideoView
{
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);

        // Re-assign to trigger VideoView.Attach() now that the handle exists
        var player = MediaPlayer;
        if (player != null)
        {
            MediaPlayer = null;
            MediaPlayer = player;
        }

        return handle;
    }
}
