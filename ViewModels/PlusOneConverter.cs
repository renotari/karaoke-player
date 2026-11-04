using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace KaraokePlayer.ViewModels;

/// <summary>Shows a 0-based index as a 1-based number.</summary>
public class PlusOneConverter : IValueConverter
{
    public static readonly PlusOneConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? (i + 1).ToString(culture) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
