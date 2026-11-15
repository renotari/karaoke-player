using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using KaraokePlayer.Services.Midi;

namespace KaraokePlayer.Views;

/// <summary>
/// Karaoke lyrics of a .kar/.mid file: the line being sung fills with color syllable
/// by syllable, with the previous line (dimmed) above and the next two below.
/// <see cref="Position"/> comes from the player a few times a second: in between the
/// time is advanced with a stopwatch so the fill moves smoothly.
/// </summary>
public class LyricsView : Control
{
    public static readonly StyledProperty<KaraokeLyrics?> LyricsProperty =
        AvaloniaProperty.Register<LyricsView, KaraokeLyrics?>(nameof(Lyrics));

    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<LyricsView, double>(nameof(Position));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<LyricsView, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LyricsView, string?>(nameof(Title));

    public KaraokeLyrics? Lyrics { get => GetValue(LyricsProperty); set => SetValue(LyricsProperty, value); }
    public double Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    private static readonly IBrush Sung = new SolidColorBrush(Color.Parse("#FFD54F"));
    private static readonly IBrush SungDim = new SolidColorBrush(Color.Parse("#80FFD54F"));
    private static readonly IBrush ToSing = Brushes.White;
    private static readonly IBrush Upcoming = new SolidColorBrush(Color.Parse("#B3FFFFFF"));
    private static readonly IBrush TitleBrush = new SolidColorBrush(Color.Parse("#80FFFFFF"));
    private static readonly Typeface LineFont = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
    private static readonly Typeface TitleFont = new(FontFamily.Default);

    /// <summary>After this long without a new syllable the next line is shown as the current one.</summary>
    private const double AdvanceAfter = 2.0;
    private const double CountdownSeconds = 3.0;

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _sinceUpdate = new();
    private double _lastShown;

    static LyricsView()
    {
        AffectsRender<LyricsView>(LyricsProperty, TitleProperty);
    }

    public LyricsView()
    {
        ClipToBounds = true;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => InvalidateVisual());
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PositionProperty)
        {
            _sinceUpdate.Restart();
            if (Position < _lastShown - 0.5)
                _lastShown = Position; // seek back or a new song
            InvalidateVisual();
        }
        else if (change.Property == LyricsProperty)
        {
            _lastShown = 0;
        }

        if (change.Property == IsPlayingProperty || change.Property == LyricsProperty || change.Property == IsVisibleProperty)
            UpdateTimer();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    private void UpdateTimer()
    {
        var run = IsPlaying && IsVisible && Lyrics?.HasLyrics == true && VisualRoot != null;
        if (run && !_timer.IsEnabled) _timer.Start();
        else if (!run && _timer.IsEnabled) _timer.Stop();
    }

    /// <summary>Player time plus the time since it was reported (never going back a little).</summary>
    private double CurrentTime()
    {
        var t = Position;
        if (IsPlaying)
            t += Math.Min(_sinceUpdate.Elapsed.TotalSeconds, 1.0);
        if (t < _lastShown && t > _lastShown - 0.5)
            t = _lastShown; // the player reported slightly less than shown: wait for it
        _lastShown = t;
        return t;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Black, bounds);

        var lyrics = Lyrics;
        if (lyrics == null || !lyrics.HasLyrics || bounds.Width < 50 || bounds.Height < 50)
            return;

        var t = CurrentTime();
        var margin = Math.Max(16, bounds.Width * 0.04);
        var width = bounds.Width - 2 * margin;
        var fontSize = Math.Clamp(bounds.Height / 9, 14, 96);

        if (!string.IsNullOrWhiteSpace(Title))
        {
            var title = Text(Title!, TitleFont, fontSize * 0.4, TitleBrush);
            context.DrawText(title, new Point((bounds.Width - title.Width) / 2, margin * 0.5));
        }

        // The line shown as current: the one being sung, or the next one after a pause
        var current = lyrics.LineAt(t);
        var waiting = current < 0;
        if (current >= 0 && current + 1 < lyrics.Lines.Count
            && t > lyrics.Lines[current].LastSyllableStart + AdvanceAfter)
        {
            current++;
            waiting = true;
        }
        if (current < 0) current = 0;

        var rowHeight = fontSize * 1.45;
        var top = (bounds.Height - rowHeight * 4) / 2;
        for (var row = 0; row < 4; row++)
        {
            var index = current - 1 + row;
            if (index < 0 || index >= lyrics.Lines.Count)
                continue;

            var line = lyrics.Lines[index];
            var y = top + row * rowHeight;
            if (row == 0 && waiting)
                continue; // its place is taken by the countdown
            if (row == 0)
                DrawLine(context, line, double.MaxValue, fontSize * 0.75, width, margin, y + fontSize * 0.25, SungDim, SungDim);
            else if (row == 1)
                DrawLine(context, line, waiting ? double.MinValue : t, fontSize, width, margin, y, ToSing, Sung);
            else
                DrawLine(context, line, double.MinValue, fontSize * 0.85, width, margin, y + fontSize * 0.1, Upcoming, Upcoming);
        }

        // Countdown dots before a line that follows a pause
        if (waiting)
        {
            var left = lyrics.Lines[current].Start - t;
            if (left is > 0 and <= CountdownSeconds)
            {
                var dots = string.Concat(Enumerable.Repeat("● ", (int)Math.Ceiling(left))).TrimEnd();
                var countdown = Text(dots, LineFont, fontSize * 0.5, Sung);
                context.DrawText(countdown, new Point((bounds.Width - countdown.Width) / 2, top + fontSize * 0.5));
            }
        }
    }

    /// <summary>Draws a centered line; the part sung by time <paramref name="t"/> in <paramref name="sungBrush"/>.</summary>
    private static void DrawLine(DrawingContext context, LyricLine line, double t, double fontSize, double width,
        double margin, double y, IBrush brush, IBrush sungBrush)
    {
        var text = line.Text.Trim();
        if (text.Length == 0)
            return;

        // Long lines shrink to fit the width
        var full = Text(text, LineFont, fontSize, brush);
        if (full.WidthIncludingTrailingWhitespace > width)
        {
            fontSize *= width / full.WidthIncludingTrailingWhitespace;
            full = Text(text, LineFont, fontSize, brush);
        }

        var x = margin + (width - full.Width) / 2;
        // (a huge clip rectangle draws nothing: clamp to the line)
        var sungWidth = Math.Min(SungWidth(line, t, text, fontSize), full.WidthIncludingTrailingWhitespace + 1);
        if (sungWidth < full.WidthIncludingTrailingWhitespace)
            context.DrawText(full, new Point(x, y));
        if (sungWidth <= 0)
            return;

        using (context.PushClip(new Rect(x, y, sungWidth, fontSize * 1.5)))
        {
            context.DrawText(Text(text, LineFont, fontSize, sungBrush), new Point(x, y));
        }
    }

    /// <summary>Width of the text sung at time <paramref name="t"/> (the current syllable fills gradually).</summary>
    private static double SungWidth(LyricLine line, double t, string trimmed, double fontSize)
    {
        if (t == double.MaxValue) return double.MaxValue;
        if (t < line.Start) return 0;

        var syllables = line.Syllables;
        var k = 0;
        while (k + 1 < syllables.Count && syllables[k + 1].Time <= t) k++;

        // The syllable lasts until the next one, at most 1.5 s (held notes are not tracked)
        var end = k + 1 < syllables.Count ? syllables[k + 1].Time : syllables[k].Time + 1.0;
        var fraction = Math.Clamp((t - syllables[k].Time) / Math.Clamp(end - syllables[k].Time, 0.05, 1.5), 0, 1);

        var leading = line.Text.Length - line.Text.TrimStart().Length;
        var before = string.Concat(syllables.Take(k).Select(s => s.Text));
        var through = before + syllables[k].Text;
        double Width(string s)
        {
            var visible = s.Length > leading ? s[leading..] : string.Empty;
            visible = visible.Length > trimmed.Length ? trimmed : visible;
            return visible.Length == 0 ? 0 : Text(visible, LineFont, fontSize, Brushes.White).WidthIncludingTrailingWhitespace;
        }

        var w0 = Width(before);
        var w1 = Width(through);
        return w0 + (w1 - w0) * fraction;
    }

    private static FormattedText Text(string text, Typeface font, double size, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, font, size, brush);
}
