using System;
using System.Collections.Generic;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Services;

/// <summary>Total and remaining time of the current playlist, in seconds.</summary>
/// <param name="Total">Sum of the known durations, the song playing included.</param>
/// <param name="Remaining">What is left: the rest of the song playing plus the queue.</param>
/// <param name="UnknownCount">Songs whose duration is not known yet (not counted).</param>
public readonly record struct PlaylistDurationSummary(double Total, double Remaining, int UnknownCount)
{
    /// <summary>"Total 1:02:03 · Remaining 45:10 · 2 without length" (empty for an empty list).</summary>
    public string Format(bool isPlaying, int songCount)
    {
        if (songCount == 0)
            return string.Empty;

        var text = $"Total {SecondsToTimeConverter.Format(Total)}";
        if (isPlaying)
            text += $" · Remaining {SecondsToTimeConverter.Format(Remaining)}";
        if (UnknownCount > 0)
            text += $" · {UnknownCount} without length";
        return text;
    }
}

public static class PlaylistDuration
{
    /// <summary>
    /// <paramref name="durations"/> are the playlist songs in order (null or 0: not
    /// known yet). When <paramref name="firstIsPlaying"/>, the first one is the song
    /// playing: its length comes from the player when known (more accurate than the
    /// tags) and only what is after <paramref name="position"/> is remaining.
    /// </summary>
    public static PlaylistDurationSummary Compute(IReadOnlyList<double?> durations,
        bool firstIsPlaying, double position, double playerDuration)
    {
        double total = 0, remaining = 0;
        var unknown = 0;

        for (var i = 0; i < durations.Count; i++)
        {
            var length = durations[i] ?? 0;
            var playing = i == 0 && firstIsPlaying;
            if (playing && playerDuration > 0)
                length = playerDuration;

            if (length <= 0 || double.IsNaN(length))
            {
                unknown++;
                continue;
            }

            total += length;
            remaining += playing ? Math.Max(0, length - Math.Max(0, position)) : length;
        }

        return new PlaylistDurationSummary(total, remaining, unknown);
    }
}
