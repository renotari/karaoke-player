using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// Converts a thumbnail file path into a decoded Bitmap (Image.Source does not accept
/// a plain path string from a binding). Decoded bitmaps are cached by path.
/// </summary>
public class ThumbnailConverter : IValueConverter
{
    public static readonly ThumbnailConverter Instance = new();

    private const int DecodeWidth = 160;
    private const int MaxCacheEntries = 1000;
    private static readonly Dictionary<string, Bitmap> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        if (Cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = Bitmap.DecodeToWidth(stream, DecodeWidth);
            if (Cache.Count >= MaxCacheEntries)
                Cache.Clear();
            Cache[path] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
