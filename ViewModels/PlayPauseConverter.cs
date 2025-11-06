using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// Converts boolean IsPlaying state to Play/Pause button text
/// </summary>
public class PlayPauseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
        {
            return isPlaying ? "⏸" : "▶";
        }
        return "▶";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
