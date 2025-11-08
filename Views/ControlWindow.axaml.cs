using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KaraokePlayer.ViewModels;
using KaraokePlayer.Services;
using System;

namespace KaraokePlayer.Views
{
    public partial class ControlWindow : Window
    {
        private readonly IWindowManager? _windowManager;

        // Design-time constructor
        public ControlWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        // Runtime constructor with dependency injection
        public ControlWindow(MainWindowViewModel viewModel, IWindowManager windowManager)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            DataContext = viewModel;
            _windowManager = windowManager;

            // Subscribe to window events for state persistence
            PositionChanged += OnPositionChanged;
            Resized += OnResized;
            Closing += OnClosing;

            // Subscribe to WindowManager messages
            if (_windowManager != null)
            {
                _windowManager.Subscribe<ToggleFullscreenMessage>(OnToggleFullscreen);
                _windowManager.Subscribe<RestoreWindowStateMessage>(OnRestoreWindowState);
            }

            // Restore window state if available
            RestoreWindowState();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPositionChanged(object? sender, PixelPointEventArgs e)
        {
            SaveWindowState();
        }

        private void OnResized(object? sender, WindowResizedEventArgs e)
        {
            SaveWindowState();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
        }

        private void SaveWindowState()
        {
            if (_windowManager == null) return;

            var state = new Services.WindowState
            {
                WindowId = "ControlWindow",
                X = Position.X,
                Y = Position.Y,
                Width = Width,
                Height = Height,
                IsMaximized = WindowState == Avalonia.Controls.WindowState.Maximized
            };

            // Cast to concrete type to access UpdateWindowState
            if (_windowManager is WindowManager windowManager)
            {
                windowManager.UpdateWindowState(state);
            }
        }

        private void RestoreWindowState()
        {
            // Window state will be restored via message from WindowManager
            // This is called during initialization
        }

        private void OnToggleFullscreen(ToggleFullscreenMessage message)
        {
            if (message.WindowId != "ControlWindow") return;

            if (message.IsFullscreen)
            {
                WindowState = Avalonia.Controls.WindowState.FullScreen;
            }
            else
            {
                WindowState = Avalonia.Controls.WindowState.Normal;
            }
        }

        private void OnRestoreWindowState(RestoreWindowStateMessage message)
        {
            if (message.WindowId != "ControlWindow") return;

            var state = message.State;
            if (state != null)
            {
                Position = new PixelPoint((int)Math.Round(state.X), (int)Math.Round(state.Y));
                Width = state.Width;
                Height = state.Height;

                if (state.IsMaximized)
                {
                    WindowState = Avalonia.Controls.WindowState.Maximized;
                }
            }
        }
    }
}
