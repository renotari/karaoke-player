using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using KaraokePlayer.ViewModels;
using LibVLCSharp.Avalonia;
using System;

namespace KaraokePlayer.Views;

public partial class PlaybackWindow : Window
{
    private DispatcherTimer? _visualizationTimer;
    private bool _isFullscreen = false;
    private WindowState _previousWindowState;
    private Size _previousSize;
    private PixelPoint _previousPosition;

    public PlaybackWindow()
    {
        InitializeComponent();
        
        // Add keyboard event handler for shortcuts
        KeyDown += OnKeyDown;
        
        // Wire up events after initialization
        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
        
        // Handle double-click for fullscreen toggle
        DoubleTapped += OnDoubleTapped;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is PlaybackWindowViewModel viewModel)
        {
            // Get the VideoView control and bind it to the ViewModel
            var videoView = this.FindControl<VideoView>("VideoView");
            if (videoView != null && viewModel.MediaPlayer != null)
            {
                videoView.MediaPlayer = viewModel.MediaPlayer;
            }

            // Subscribe to ViewModel events
            viewModel.FullscreenRequested += OnFullscreenRequested;
            viewModel.VisualizationUpdateRequested += OnVisualizationUpdateRequested;

            // Start visualization timer if needed
            if (viewModel.IsAudioContent)
            {
                StartVisualizationTimer();
            }

            // Subscribe to content type changes
            viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(PlaybackWindowViewModel.IsAudioContent))
                {
                    if (viewModel.IsAudioContent)
                    {
                        StartVisualizationTimer();
                    }
                    else
                    {
                        StopVisualizationTimer();
                    }
                }
            };
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopVisualizationTimer();

        if (DataContext is PlaybackWindowViewModel viewModel)
        {
            viewModel.FullscreenRequested -= OnFullscreenRequested;
            viewModel.VisualizationUpdateRequested -= OnVisualizationUpdateRequested;
            
            // Cleanup ViewModel subscriptions
            viewModel.Cleanup();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not PlaybackWindowViewModel viewModel)
            return;

        // Handle keyboard shortcuts
        switch (e.Key)
        {
            case Key.F11:
            case Key.F:
                viewModel.ToggleFullscreenCommand.Execute(System.Reactive.Unit.Default);
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isFullscreen)
                {
                    viewModel.ToggleFullscreenCommand.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                }
                break;

            case Key.Space:
                // Forward to main window via message bus
                viewModel.SendPlayPauseCommand();
                e.Handled = true;
                break;

            case Key.Right:
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    viewModel.SendNextCommand();
                    e.Handled = true;
                }
                break;

            case Key.Left:
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    viewModel.SendPreviousCommand();
                    e.Handled = true;
                }
                break;

            case Key.Up:
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    viewModel.SendVolumeUpCommand();
                    e.Handled = true;
                }
                break;

            case Key.Down:
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    viewModel.SendVolumeDownCommand();
                    e.Handled = true;
                }
                break;

            case Key.M:
                viewModel.SendMuteCommand();
                e.Handled = true;
                break;

            case Key.S:
                if (e.KeyModifiers == KeyModifiers.Control)
                {
                    viewModel.ToggleSubtitlesCommand.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PlaybackWindowViewModel viewModel)
        {
            viewModel.ToggleFullscreenCommand.Execute(System.Reactive.Unit.Default);
        }
    }

    private void OnFullscreenRequested(object? sender, bool enterFullscreen)
    {
        if (enterFullscreen && !_isFullscreen)
        {
            EnterFullscreen();
        }
        else if (!enterFullscreen && _isFullscreen)
        {
            ExitFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        // Save current window state
        _previousWindowState = WindowState;
        _previousSize = ClientSize;
        _previousPosition = Position;

        // Enter fullscreen
        WindowState = WindowState.FullScreen;
        _isFullscreen = true;
    }

    private void ExitFullscreen()
    {
        // Restore previous window state
        WindowState = _previousWindowState;
        
        // Restore size and position if not maximized
        if (_previousWindowState != WindowState.Maximized)
        {
            ClientSize = _previousSize;
            Position = _previousPosition;
        }

        _isFullscreen = false;
    }

    private void StartVisualizationTimer()
    {
        if (_visualizationTimer != null)
            return;

        // Create timer for 30 FPS visualization updates
        _visualizationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0)
        };

        _visualizationTimer.Tick += (s, e) =>
        {
            if (DataContext is PlaybackWindowViewModel viewModel)
            {
                UpdateVisualization(viewModel);
            }
        };

        _visualizationTimer.Start();
    }

    private void StopVisualizationTimer()
    {
        if (_visualizationTimer != null)
        {
            _visualizationTimer.Stop();
            _visualizationTimer = null;
        }
    }

    private void OnVisualizationUpdateRequested(object? sender, EventArgs e)
    {
        if (DataContext is PlaybackWindowViewModel viewModel)
        {
            UpdateVisualization(viewModel);
        }
    }

    private void UpdateVisualization(PlaybackWindowViewModel viewModel)
    {
        // Get the visualization canvas
        var canvas = this.FindControl<Canvas>("VisualizationCanvas");
        if (canvas == null)
            return;

        // Get audio spectrum data from ViewModel
        var spectrum = viewModel.GetAudioSpectrum();
        if (spectrum == null || spectrum.Length == 0)
            return;

        // Clear previous visualization
        canvas.Children.Clear();

        // Render visualization based on current style
        switch (viewModel.VisualizationStyle)
        {
            case "bars":
                RenderBarsVisualization(canvas, spectrum);
                break;
            case "waveform":
                RenderWaveformVisualization(canvas, spectrum);
                break;
            case "circular":
                RenderCircularVisualization(canvas, spectrum);
                break;
            case "particles":
                RenderParticlesVisualization(canvas, spectrum);
                break;
            default:
                RenderBarsVisualization(canvas, spectrum);
                break;
        }
    }

    private void RenderBarsVisualization(Canvas canvas, float[] spectrum)
    {
        // Simple bars visualization
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;
        
        if (width <= 0 || height <= 0)
            return;

        var barCount = Math.Min(spectrum.Length, 64);
        var barWidth = width / barCount;
        var maxHeight = height * 0.8;

        for (int i = 0; i < barCount; i++)
        {
            var value = spectrum[i * spectrum.Length / barCount];
            var barHeight = value * maxHeight;

            var bar = new Border
            {
                Width = barWidth - 2,
                Height = barHeight,
                Background = Avalonia.Media.Brushes.White,
                Opacity = 0.8
            };

            Canvas.SetLeft(bar, i * barWidth);
            Canvas.SetBottom(bar, 0);

            canvas.Children.Add(bar);
        }
    }

    private void RenderWaveformVisualization(Canvas canvas, float[] spectrum)
    {
        // Waveform visualization - simplified implementation
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;
        
        if (width <= 0 || height <= 0)
            return;

        var centerY = height / 2;
        var pointCount = Math.Min(spectrum.Length, 128);
        var pointSpacing = width / pointCount;

        for (int i = 0; i < pointCount - 1; i++)
        {
            var value1 = spectrum[i * spectrum.Length / pointCount];
            var value2 = spectrum[(i + 1) * spectrum.Length / pointCount];

            var y1 = centerY + (value1 - 0.5) * height * 0.8;
            var y2 = centerY + (value2 - 0.5) * height * 0.8;

            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(i * pointSpacing, y1),
                EndPoint = new Avalonia.Point((i + 1) * pointSpacing, y2),
                Stroke = Avalonia.Media.Brushes.White,
                StrokeThickness = 2,
                Opacity = 0.8
            };

            canvas.Children.Add(line);
        }
    }

    private void RenderCircularVisualization(Canvas canvas, float[] spectrum)
    {
        // Circular visualization - simplified implementation
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;
        
        if (width <= 0 || height <= 0)
            return;

        var centerX = width / 2;
        var centerY = height / 2;
        var radius = Math.Min(width, height) * 0.3;
        var barCount = Math.Min(spectrum.Length, 64);

        for (int i = 0; i < barCount; i++)
        {
            var value = spectrum[i * spectrum.Length / barCount];
            var angle = (i * 360.0 / barCount) * Math.PI / 180.0;
            var barLength = value * radius;

            var x1 = centerX + Math.Cos(angle) * radius;
            var y1 = centerY + Math.Sin(angle) * radius;
            var x2 = centerX + Math.Cos(angle) * (radius + barLength);
            var y2 = centerY + Math.Sin(angle) * (radius + barLength);

            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(x1, y1),
                EndPoint = new Avalonia.Point(x2, y2),
                Stroke = Avalonia.Media.Brushes.White,
                StrokeThickness = 3,
                Opacity = 0.8
            };

            canvas.Children.Add(line);
        }
    }

    private void RenderParticlesVisualization(Canvas canvas, float[] spectrum)
    {
        // Particles visualization - simplified implementation
        // For a full implementation, this would maintain particle state between frames
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;
        
        if (width <= 0 || height <= 0)
            return;

        var random = new Random();
        var particleCount = Math.Min(spectrum.Length, 100);

        for (int i = 0; i < particleCount; i++)
        {
            var value = spectrum[i * spectrum.Length / particleCount];
            var size = 4 + value * 20;

            var particle = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Fill = Avalonia.Media.Brushes.White,
                Opacity = value * 0.8
            };

            Canvas.SetLeft(particle, random.NextDouble() * width);
            Canvas.SetTop(particle, random.NextDouble() * height);

            canvas.Children.Add(particle);
        }
    }
}
