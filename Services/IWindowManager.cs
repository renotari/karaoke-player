using System;
using System.Threading.Tasks;

namespace KaraokePlayer.Services
{
    /// <summary>
    /// Manages window modes, states, and cross-window communication
    /// </summary>
    public interface IWindowManager
    {
        /// <summary>
        /// Gets the current display mode
        /// </summary>
        WindowMode CurrentMode { get; }

        /// <summary>
        /// Switches between single and dual screen modes
        /// </summary>
        /// <param name="mode">The target window mode</param>
        Task SetModeAsync(WindowMode mode);

        /// <summary>
        /// Opens the Playlist Composer window
        /// </summary>
        Task OpenPlaylistComposerAsync();

        /// <summary>
        /// Closes the Playlist Composer window if open
        /// </summary>
        void ClosePlaylistComposer();

        /// <summary>
        /// Toggles fullscreen mode for the specified window
        /// </summary>
        /// <param name="windowId">The window identifier</param>
        void ToggleFullscreen(string windowId);

        /// <summary>
        /// Checks if a window is in fullscreen mode
        /// </summary>
        /// <param name="windowId">The window identifier</param>
        bool IsFullscreen(string windowId);

        /// <summary>
        /// Saves the current window states (position, size) to settings
        /// </summary>
        Task SaveWindowStateAsync();

        /// <summary>
        /// Restores window states from settings
        /// </summary>
        Task RestoreWindowStateAsync();

        /// <summary>
        /// Broadcasts a message to all windows via message bus
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        void BroadcastMessage<T>(T message) where T : class;

        /// <summary>
        /// Subscribes to messages of a specific type
        /// </summary>
        /// <param name="handler">The message handler</param>
        /// <returns>Disposable subscription</returns>
        IDisposable Subscribe<T>(Action<T> handler) where T : class;

        /// <summary>
        /// Event raised when the window mode changes
        /// </summary>
        event EventHandler<WindowModeChangedEventArgs>? ModeChanged;

        /// <summary>
        /// Event raised when a window's fullscreen state changes
        /// </summary>
        event EventHandler<FullscreenChangedEventArgs>? FullscreenChanged;
    }

    /// <summary>
    /// Window display modes
    /// </summary>
    public enum WindowMode
    {
        /// <summary>
        /// Single window mode with all functionality in one window
        /// </summary>
        Single,

        /// <summary>
        /// Dual window mode with separate playback and control windows
        /// </summary>
        Dual
    }

    /// <summary>
    /// Event args for window mode changes
    /// </summary>
    public class WindowModeChangedEventArgs : EventArgs
    {
        public WindowMode OldMode { get; set; }
        public WindowMode NewMode { get; set; }
    }

    /// <summary>
    /// Event args for fullscreen state changes
    /// </summary>
    public class FullscreenChangedEventArgs : EventArgs
    {
        public string WindowId { get; set; } = string.Empty;
        public bool IsFullscreen { get; set; }
    }

    /// <summary>
    /// Window state for persistence
    /// </summary>
    public class WindowState
    {
        public string WindowId { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }
}
