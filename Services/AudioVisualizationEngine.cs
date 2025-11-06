using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace KaraokePlayer.Services;

/// <summary>
/// Implements audio visualization rendering with multiple styles
/// </summary>
public class AudioVisualizationEngine : IAudioVisualizationEngine
{
    private VisualizationStyle _style = VisualizationStyle.Bars;
    private float[] _spectrumData = Array.Empty<float>();
    private float[] _smoothedSpectrum = Array.Empty<float>();
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private SKBitmap? _artwork;
    private readonly object _dataLock = new();
    
    // Animation state
    private float _rotation = 0f;
    private readonly Random _random = new();
    private readonly List<Particle> _particles = new();
    private const int MaxParticles = 100;
    private const float SmoothingFactor = 0.7f;

    public VisualizationStyle Style
    {
        get => _style;
        set => _style = value;
    }

    public void UpdateAudioData(float[] spectrumData)
    {
        if (spectrumData == null || spectrumData.Length == 0)
            return;

        lock (_dataLock)
        {
            _spectrumData = new float[spectrumData.Length];
            Array.Copy(spectrumData, _spectrumData, spectrumData.Length);

            // Initialize smoothed spectrum if needed
            if (_smoothedSpectrum.Length != spectrumData.Length)
            {
                _smoothedSpectrum = new float[spectrumData.Length];
                Array.Copy(spectrumData, _smoothedSpectrum, spectrumData.Length);
            }
            else
            {
                // Apply smoothing for more fluid animations
                for (int i = 0; i < spectrumData.Length; i++)
                {
                    _smoothedSpectrum[i] = _smoothedSpectrum[i] * SmoothingFactor + 
                                          spectrumData[i] * (1 - SmoothingFactor);
                }
            }
        }
    }

    public void Render(SKCanvas canvas, int width, int height)
    {
        if (canvas == null || width <= 0 || height <= 0)
            return;

        canvas.Clear(SKColors.Black);

        // Draw background artwork if available
        DrawBackground(canvas, width, height);

        // Draw song info
        DrawSongInfo(canvas, width, height);

        // Draw visualization based on style
        lock (_dataLock)
        {
            if (_smoothedSpectrum.Length == 0)
                return;

            switch (_style)
            {
                case VisualizationStyle.Bars:
                    RenderBars(canvas, width, height);
                    break;
                case VisualizationStyle.Waveform:
                    RenderWaveform(canvas, width, height);
                    break;
                case VisualizationStyle.Circular:
                    RenderCircular(canvas, width, height);
                    break;
                case VisualizationStyle.Particles:
                    RenderParticles(canvas, width, height);
                    break;
            }
        }
    }

    public void SetSongInfo(string title, string artist, string? artworkPath = null)
    {
        _title = title ?? string.Empty;
        _artist = artist ?? string.Empty;

        // Load artwork if provided
        if (!string.IsNullOrEmpty(artworkPath) && File.Exists(artworkPath))
        {
            try
            {
                _artwork?.Dispose();
                _artwork = SKBitmap.Decode(artworkPath);
            }
            catch
            {
                _artwork = null;
            }
        }
        else
        {
            _artwork?.Dispose();
            _artwork = null;
        }
    }

    public void Clear()
    {
        lock (_dataLock)
        {
            _spectrumData = Array.Empty<float>();
            _smoothedSpectrum = Array.Empty<float>();
        }
        _title = string.Empty;
        _artist = string.Empty;
        _artwork?.Dispose();
        _artwork = null;
        _particles.Clear();
    }

    private void DrawBackground(SKCanvas canvas, int width, int height)
    {
        if (_artwork == null)
            return;

        // Draw artwork as blurred background
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        // Calculate scaling to cover entire canvas
        float scaleX = (float)width / _artwork.Width;
        float scaleY = (float)height / _artwork.Height;
        float scale = Math.Max(scaleX, scaleY);

        int scaledWidth = (int)(_artwork.Width * scale);
        int scaledHeight = (int)(_artwork.Height * scale);
        int x = (width - scaledWidth) / 2;
        int y = (height - scaledHeight) / 2;

        var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);

        // Draw with reduced opacity for background effect
        paint.Color = paint.Color.WithAlpha(40);
        canvas.DrawBitmap(_artwork, destRect, paint);

        // Add dark overlay for better text/visualization visibility
        using var overlayPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 180)
        };
        canvas.DrawRect(0, 0, width, height, overlayPaint);
    }

    private void DrawSongInfo(SKCanvas canvas, int width, int height)
    {
        if (string.IsNullOrEmpty(_title) && string.IsNullOrEmpty(_artist))
            return;

        using var titlePaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 48,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        using var artistPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            TextSize = 32,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
        };

        // Draw title
        if (!string.IsNullOrEmpty(_title))
        {
            float titleWidth = titlePaint.MeasureText(_title);
            float titleX = (width - titleWidth) / 2;
            float titleY = height * 0.15f;
            canvas.DrawText(_title, titleX, titleY, titlePaint);
        }

        // Draw artist
        if (!string.IsNullOrEmpty(_artist))
        {
            float artistWidth = artistPaint.MeasureText(_artist);
            float artistX = (width - artistWidth) / 2;
            float artistY = height * 0.15f + 50;
            canvas.DrawText(_artist, artistX, artistY, artistPaint);
        }
    }

    private void RenderBars(SKCanvas canvas, int width, int height)
    {
        int barCount = Math.Min(_smoothedSpectrum.Length, 64);
        float barWidth = (float)width / barCount;
        float maxHeight = height * 0.6f;
        float baseY = height * 0.8f;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        for (int i = 0; i < barCount; i++)
        {
            float amplitude = _smoothedSpectrum[i * _smoothedSpectrum.Length / barCount];
            float barHeight = amplitude * maxHeight;

            // Color gradient based on amplitude
            byte r = (byte)(100 + amplitude * 155);
            byte g = (byte)(50 + amplitude * 100);
            byte b = (byte)(200 - amplitude * 100);
            paint.Color = new SKColor(r, g, b);

            float x = i * barWidth;
            canvas.DrawRect(x + 2, baseY - barHeight, barWidth - 4, barHeight, paint);
        }
    }

    private void RenderWaveform(SKCanvas canvas, int width, int height)
    {
        using var path = new SKPath();
        using var paint = new SKPaint
        {
            Color = new SKColor(100, 200, 255),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };

        float centerY = height / 2f;
        float maxAmplitude = height * 0.3f;
        int pointCount = Math.Min(_smoothedSpectrum.Length, width / 4);

        path.MoveTo(0, centerY);

        for (int i = 0; i < pointCount; i++)
        {
            float x = (float)i / pointCount * width;
            float amplitude = _smoothedSpectrum[i * _smoothedSpectrum.Length / pointCount];
            float y = centerY + (amplitude - 0.5f) * maxAmplitude * 2;
            path.LineTo(x, y);
        }

        canvas.DrawPath(path, paint);

        // Draw mirrored waveform
        path.Reset();
        path.MoveTo(0, centerY);
        for (int i = 0; i < pointCount; i++)
        {
            float x = (float)i / pointCount * width;
            float amplitude = _smoothedSpectrum[i * _smoothedSpectrum.Length / pointCount];
            float y = centerY - (amplitude - 0.5f) * maxAmplitude * 2;
            path.LineTo(x, y);
        }

        canvas.DrawPath(path, paint);
    }

    private void RenderCircular(SKCanvas canvas, int width, int height)
    {
        float centerX = width / 2f;
        float centerY = height / 2f;
        float radius = Math.Min(width, height) * 0.25f;
        int barCount = Math.Min(_smoothedSpectrum.Length, 128);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Update rotation for animation
        _rotation += 0.5f;
        if (_rotation >= 360) _rotation = 0;

        // Calculate average bass for center pulse
        float bassAvg = 0;
        int bassCount = Math.Min(8, _smoothedSpectrum.Length);
        for (int i = 0; i < bassCount; i++)
        {
            bassAvg += _smoothedSpectrum[i];
        }
        bassAvg /= bassCount;

        // Draw pulsing center
        float centerRadius = radius * 0.3f * (1 + bassAvg * 0.5f);
        paint.Color = new SKColor(150, 100, 255, 200);
        canvas.DrawCircle(centerX, centerY, centerRadius, paint);

        // Draw radial bars
        for (int i = 0; i < barCount; i++)
        {
            float amplitude = _smoothedSpectrum[i * _smoothedSpectrum.Length / barCount];
            float angle = (float)(i * 360.0 / barCount + _rotation) * (float)Math.PI / 180f;
            float barLength = amplitude * radius * 0.8f;

            float x1 = centerX + (float)Math.Cos(angle) * radius;
            float y1 = centerY + (float)Math.Sin(angle) * radius;
            float x2 = centerX + (float)Math.Cos(angle) * (radius + barLength);
            float y2 = centerY + (float)Math.Sin(angle) * (radius + barLength);

            // Color based on position
            byte r = (byte)(100 + (i * 155 / barCount));
            byte g = (byte)(50 + amplitude * 150);
            byte b = (byte)(200 - (i * 100 / barCount));
            paint.Color = new SKColor(r, g, b);
            paint.StrokeWidth = 4;
            paint.Style = SKPaintStyle.Stroke;

            canvas.DrawLine(x1, y1, x2, y2, paint);
        }
    }

    private void RenderParticles(SKCanvas canvas, int width, int height)
    {
        // Calculate average amplitude for particle generation
        float avgAmplitude = 0;
        for (int i = 0; i < _smoothedSpectrum.Length; i++)
        {
            avgAmplitude += _smoothedSpectrum[i];
        }
        avgAmplitude /= _smoothedSpectrum.Length;

        // Generate new particles based on audio intensity
        if (_particles.Count < MaxParticles && avgAmplitude > 0.1f)
        {
            int newParticles = (int)(avgAmplitude * 5);
            for (int i = 0; i < newParticles && _particles.Count < MaxParticles; i++)
            {
                _particles.Add(new Particle
                {
                    X = width / 2f,
                    Y = height / 2f,
                    VelocityX = (float)(_random.NextDouble() - 0.5) * 10,
                    VelocityY = (float)(_random.NextDouble() - 0.5) * 10,
                    Size = 2 + (float)_random.NextDouble() * 4,
                    Life = 1.0f,
                    Color = GetParticleColor(avgAmplitude)
                });
            }
        }

        // Update and draw particles
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            
            // Update particle
            particle.X += particle.VelocityX;
            particle.Y += particle.VelocityY;
            particle.Life -= 0.02f;
            particle.Size *= 0.98f;

            // Remove dead particles
            if (particle.Life <= 0 || particle.Size < 0.5f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            // Draw particle
            byte alpha = (byte)(particle.Life * 255);
            paint.Color = particle.Color.WithAlpha(alpha);
            canvas.DrawCircle(particle.X, particle.Y, particle.Size, paint);
        }
    }

    private SKColor GetParticleColor(float amplitude)
    {
        // Color shifts based on amplitude
        if (amplitude > 0.7f)
            return new SKColor(255, 100, 100); // Red for high
        else if (amplitude > 0.4f)
            return new SKColor(100, 255, 100); // Green for medium
        else
            return new SKColor(100, 100, 255); // Blue for low
    }

    private class Particle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float Size { get; set; }
        public float Life { get; set; }
        public SKColor Color { get; set; }
    }
}
