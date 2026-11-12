using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// Formats a number of seconds (double) as m:ss, or h:mm:ss for long media.
/// </summary>
public class SecondsToTimeConverter : IValueConverter
{
    public static readonly SecondsToTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Format(value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            TimeSpan ts => ts.TotalSeconds,
            _ => 0
        });
    }

    /// <summary>m:ss, or h:mm:ss from one hour up (negative and NaN count as 0).</summary>
    public static string Format(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0)
            seconds = 0;

        var time = TimeSpan.FromSeconds(Math.Floor(seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
