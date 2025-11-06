using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ReactiveUI;

namespace KaraokePlayer.Services
{
    /// <summary>
    /// Manages window modes, states, and cross-window communication using ReactiveUI MessageBus
    /// </summary>
    public class WindowManager : IWindowManager
    {
        private readonly ISettingsManager _settingsManager;
        private readonly IMessageBus _messageBus;
        private readonly Dictionary<string, WindowState> _windowStates;
        private readonly Dictionary<string, bool> _fullscreenStates;
        private WindowMode _currentMode;

        public WindowMode CurrentMode => _currentMode;

        public event EventHandler<WindowModeChangedEventArgs>? ModeChanged;
        public event EventHandler<FullscreenChangedEventArgs>? FullscreenChanged;

        public WindowManager(ISettingsManager settingsManager, IMessageBus? messageBus = null)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _messageBus = messageBus ?? ReactiveUI.MessageBus.Current;
            _windowStates = new Dictionary<string, WindowState>();
            _fullscreenStates = new Dictionary<string, bool>();
            
            // Load initial mode from settings
            var displayMode = _settingsManager.GetSetting<string>("DisplayMode") ?? "Single";
            _currentMode = Enum.TryParse<WindowMode>(displayMode, true, out var mode) ? mode : WindowMode.Single;
        }

        /// <summary>
        /// Switches between single and dual screen modes
        /// </summary>
        public async Task SetModeAsync(WindowMode mode)
        {
            if (_currentMode == mode)
                return;

            var oldMode = _currentMode;
            _currentMode = mode;

            // Save mode to settings
            _settingsManager.SetSetting("DisplayMode", mode.ToString());
            await _settingsManager.SaveSettingsAsync();

            // Broadcast mode change message
            BroadcastMessage(new WindowModeChangeMessage
            {
                OldMode = oldMode,
                NewMode = mode
            });

            // Raise event
            ModeChanged?.Invoke(this, new WindowModeChangedEventArgs
            {
                OldMode = oldMode,
                NewMode = mode
            });
        }

        /// <summary>
        /// Opens the Playlist Composer window
        /// </summary>
        public async Task OpenPlaylistComposerAsync()
        {
            // Broadcast message to open playlist composer
            BroadcastMessage(new OpenPlaylistComposerMessage());
            
            // Restore window state if available
            if (_windowStates.TryGetValue("PlaylistComposer", out var state))
            {
                BroadcastMessage(new RestoreWindowStateMessage
                {
                    WindowId = "PlaylistComposer",
                    State = state
                });
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Closes the Playlist Composer window if open
        /// </summary>
        public void ClosePlaylistComposer()
        {
            BroadcastMessage(new ClosePlaylistComposerMessage());
        }

        /// <summary>
        /// Toggles fullscreen mode for the specified window
        /// </summary>
        public void ToggleFullscreen(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
                throw new ArgumentException("Window ID cannot be null or empty", nameof(windowId));

            var isCurrentlyFullscreen = _fullscreenStates.TryGetValue(windowId, out var state) && state;
            var newFullscreenState = !isCurrentlyFullscreen;

            _fullscreenStates[windowId] = newFullscreenState;

            // Broadcast fullscreen toggle message
            BroadcastMessage(new ToggleFullscreenMessage
            {
                WindowId = windowId,
                IsFullscreen = newFullscreenState
            });

            // Raise event
            FullscreenChanged?.Invoke(this, new FullscreenChangedEventArgs
            {
                WindowId = windowId,
                IsFullscreen = newFullscreenState
            });
        }

        /// <summary>
        /// Checks if a window is in fullscreen mode
        /// </summary>
        public bool IsFullscreen(string windowId)
        {
            return _fullscreenStates.TryGetValue(windowId, out var state) && state;
        }

        /// <summary>
        /// Saves the current window states (position, size) to settings
        /// </summary>
        public async Task SaveWindowStateAsync()
        {
            var states = _windowStates.Values.ToList();
            _settingsManager.SetSetting("WindowStates", states);
            await _settingsManager.SaveSettingsAsync();
        }

        /// <summary>
        /// Restores window states from settings
        /// </summary>
        public async Task RestoreWindowStateAsync()
        {
            var states = _settingsManager.GetSetting<List<WindowState>>("WindowStates");
            
            if (states != null)
            {
                _windowStates.Clear();
                foreach (var state in states)
                {
                    _windowStates[state.WindowId] = state;
                    
                    // Broadcast restore message for each window
                    BroadcastMessage(new RestoreWindowStateMessage
                    {
                        WindowId = state.WindowId,
                        State = state
                    });
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Updates a window's state (called by windows when they move/resize)
        /// </summary>
        public void UpdateWindowState(WindowState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _windowStates[state.WindowId] = state;
        }

        /// <summary>
        /// Broadcasts a message to all windows via ReactiveUI MessageBus
        /// </summary>
        public void BroadcastMessage<T>(T message) where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _messageBus.SendMessage(message);
        }

        /// <summary>
        /// Subscribes to messages of a specific type
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return _messageBus.Listen<T>().Subscribe(handler);
        }
    }

    #region Message Types

    /// <summary>
    /// Message broadcast when window mode changes
    /// </summary>
    public class WindowModeChangeMessage
    {
        public WindowMode OldMode { get; set; }
        public WindowMode NewMode { get; set; }
    }

    /// <summary>
    /// Message to open the Playlist Composer window
    /// </summary>
    public class OpenPlaylistComposerMessage
    {
    }

    /// <summary>
    /// Message to close the Playlist Composer window
    /// </summary>
    public class ClosePlaylistComposerMessage
    {
    }

    /// <summary>
    /// Message to toggle fullscreen for a window
    /// </summary>
    public class ToggleFullscreenMessage
    {
        public string WindowId { get; set; } = string.Empty;
        public bool IsFullscreen { get; set; }
    }

    /// <summary>
    /// Message to restore a window's state
    /// </summary>
    public class RestoreWindowStateMessage
    {
        public string WindowId { get; set; } = string.Empty;
        public WindowState State { get; set; } = new WindowState();
    }

    /// <summary>
    /// Message to synchronize state across windows
    /// </summary>
    public class StateSyncMessage
    {
        public string StateKey { get; set; } = string.Empty;
        public object? StateValue { get; set; }
    }

    #endregion
}
