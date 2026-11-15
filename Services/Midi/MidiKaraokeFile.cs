using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KaraokePlayer.Services.Midi;

/// <summary>One sung piece of text (a syllable or a word) and when it starts, in seconds.</summary>
public sealed record LyricSyllable(double Time, string Text);

/// <summary>A line of lyrics as shown on screen.</summary>
public sealed class LyricLine
{
    public LyricLine(IReadOnlyList<LyricSyllable> syllables, bool startsParagraph)
    {
        Syllables = syllables;
        StartsParagraph = startsParagraph;
        Text = string.Concat(syllables.Select(s => s.Text));
    }

    public IReadOnlyList<LyricSyllable> Syllables { get; }
    public bool StartsParagraph { get; }
    public string Text { get; }
    public double Start => Syllables[0].Time;
    public double LastSyllableStart => Syllables[^1].Time;
}

/// <summary>Lyrics of a .kar/.mid file (empty when the file has none).</summary>
public sealed class KaraokeLyrics
{
    public static readonly KaraokeLyrics Empty = new(Array.Empty<LyricLine>());

    public KaraokeLyrics(IReadOnlyList<LyricLine> lines) => Lines = lines;

    public IReadOnlyList<LyricLine> Lines { get; }
    public bool HasLyrics => Lines.Count > 0;

    /// <summary>Index of the line being sung at <paramref name="time"/> (-1 before the first one).</summary>
    public int LineAt(double time)
    {
        int lo = 0, hi = Lines.Count - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (Lines[mid].Start <= time) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return found;
    }
}

/// <summary>
/// Reads what the player needs from a Standard MIDI File (.mid, .kar): lyrics with
/// their times, the "@" header of Soft Karaoke files and the length. The music
/// itself is rendered by <see cref="MidiRenderer"/>.
///
/// Lyrics come in two flavours:
/// - Soft Karaoke (.kar): Text events (FF 01) in one track; "@T", "@L", "@K", "@I"...
///   lines are header information; "/" starts a new line, "\" a new paragraph.
/// - Lyric events (FF 05), common in .mid files: "\r" ends a line, "\n" a paragraph
///   (some files use "/" and "\" here too). Text events then often hold chords.
/// Text is decoded as Windows-1252 (old files), or UTF-8 when it is valid UTF-8.
/// </summary>
public sealed class MidiKaraokeFile
{
    /// <summary>Fewer syllables than this: not lyrics (titles, markers, "stop"...).</summary>
    private const int MinSyllables = 10;

    private static readonly Encoding Latin;

    static MidiKaraokeFile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Latin = Encoding.GetEncoding(1252);
    }

    private MidiKaraokeFile() { }

    public KaraokeLyrics Lyrics { get; private set; } = KaraokeLyrics.Empty;

    /// <summary>"@T" lines of a Soft Karaoke file (usually title, then artist).</summary>
    public IReadOnlyList<string> TitleLines { get; private set; } = Array.Empty<string>();

    /// <summary>Length in seconds (the last event of any track).</summary>
    public double Duration { get; private set; }

    public static MidiKaraokeFile Load(string path) => Parse(File.ReadAllBytes(path));

    public static MidiKaraokeFile Parse(byte[] data)
    {
        var reader = new ChunkReader(data);
        if (reader.ReadId() != "MThd")
            throw new InvalidDataException("Not a MIDI file");

        var headerLength = (int)reader.ReadUInt32();
        var headerStart = reader.Position;
        reader.ReadUInt16(); // format: 0 and 1 are read the same way
        var trackCount = reader.ReadUInt16();
        var division = reader.ReadUInt16();
        reader.Position = headerStart + headerLength;
        if ((division & 0x8000) != 0)
            throw new InvalidDataException("SMPTE time division is not supported");

        var tracks = new List<List<MetaEvent>>();
        var tempos = new List<(long Tick, int MicrosPerQuarter)>();
        long lastTick = 0;

        for (var t = 0; t < trackCount && reader.Remaining >= 8; t++)
        {
            var id = reader.ReadId();
            var length = (int)reader.ReadUInt32();
            var end = Math.Min(data.Length, reader.Position + length);
            if (id != "MTrk")
            {
                reader.Position = end; // unknown chunk: skip it
                t--;
                continue;
            }

            var events = new List<MetaEvent>();
            tracks.Add(events);
            lastTick = Math.Max(lastTick, ReadTrack(reader, end, events, tempos));
            reader.Position = end;
        }

        var timing = new TempoMap(division, tempos);
        var file = new MidiKaraokeFile { Duration = timing.Seconds(lastTick) };
        file.ReadLyrics(tracks, timing);
        return file;
    }

    private sealed record MetaEvent(long Tick, byte Type, byte[] Data);

    /// <summary>Reads one track (meta events and tempo changes kept); returns its last tick.</summary>
    private static long ReadTrack(ChunkReader reader, int end, List<MetaEvent> events,
        List<(long, int)> tempos)
    {
        long tick = 0;
        byte status = 0;
        try
        {
            while (reader.Position < end)
            {
                tick += reader.ReadVarLength();
                var b = reader.PeekByte();
                if (b >= 0x80) { status = b; reader.Position++; }
                else if (status == 0) throw new InvalidDataException("Running status without a status byte");

                if (status == 0xFF)
                {
                    var type = reader.ReadByte();
                    var length = (int)reader.ReadVarLength();
                    var payload = reader.ReadBytes(length);
                    if (type == 0x51 && payload.Length == 3)
                        tempos.Add((tick, (payload[0] << 16) | (payload[1] << 8) | payload[2]));
                    else if (type is 0x01 or 0x05)
                        events.Add(new MetaEvent(tick, type, payload));
                    else if (type == 0x2F)
                        break; // end of track
                    status = 0; // meta and sysex events cancel running status
                }
                else if (status is 0xF0 or 0xF7)
                {
                    var length = (int)reader.ReadVarLength(); // read before using Position
                    reader.Position += length;
                    status = 0;
                }
                else
                {
                    var kind = status & 0xF0;
                    reader.Position += kind is 0xC0 or 0xD0 ? 1 : 2;
                }
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or InvalidDataException)
        {
            // Truncated or damaged track: keep what was read (old files often are)
        }
        return tick;
    }

    private void ReadLyrics(List<List<MetaEvent>> tracks, TempoMap timing)
    {
        // Lyric events win when there are enough of them (Text events are then chords)
        var lyricEvents = tracks.SelectMany(t => t).Where(e => e.Type == 0x05).OrderBy(e => e.Tick).ToList();
        List<MetaEvent> source;
        if (lyricEvents.Count >= MinSyllables)
        {
            source = lyricEvents;
        }
        else
        {
            // Soft Karaoke: the track with most Text events ("Words")
            source = tracks
                .Select(t => t.Where(e => e.Type == 0x01).ToList())
                .OrderByDescending(t => t.Count)
                .FirstOrDefault() ?? new List<MetaEvent>();
        }

        // Files that break lines with CR/LF use "/" and "\" as plain characters
        // ("CH1: Text/Setup"); the others use them as line and paragraph breaks
        var slashBreaks = !source.Any(e => e.Data.Any(b => b is 0x0D or 0x0A));

        var titles = new List<string>();
        var lines = new List<LyricLine>();
        var current = new List<LyricSyllable>();
        var paragraph = true;
        var syllableCount = 0;

        void EndLine(bool newParagraph)
        {
            if (current.Count > 0 && current.Any(s => s.Text.Trim().Length > 0))
            {
                var line = new LyricLine(current.ToList(), paragraph);
                if (!IsNotSung(line.Text))
                    lines.Add(line);
            }
            if (current.Count > 0 || newParagraph)
                paragraph = newParagraph;
            current.Clear();
        }

        foreach (var e in source)
        {
            var text = Decode(e.Data);
            if (text.StartsWith('@'))
            {
                if (text.Length > 2 && char.ToUpperInvariant(text[1]) == 'T')
                    titles.Add(text[2..].Trim());
                continue; // header line, not sung
            }

            var time = timing.Seconds(e.Tick);
            var piece = new StringBuilder();
            foreach (var c in text)
            {
                if (c is '\r' or '\n' || (slashBreaks && c is '/' or '\\'))
                {
                    if (piece.Length > 0) { current.Add(new LyricSyllable(time, piece.ToString())); piece.Clear(); }
                    EndLine(c is '\\' or '\n');
                }
                else if (!char.IsControl(c))
                {
                    piece.Append(c);
                }
            }
            if (piece.Length > 0)
            {
                current.Add(new LyricSyllable(time, piece.ToString()));
                syllableCount++;
            }
        }
        EndLine(false);

        TitleLines = titles;
        Lyrics = syllableCount >= MinSyllables && lines.Count > 0 ? new KaraokeLyrics(lines) : KaraokeLyrics.Empty;
    }

    /// <summary>
    /// Lines that are in many files but are not sung: the copyright notice of
    /// Soft Karaoke files and the channel list of some .mid files ("CH4: Melodia").
    /// </summary>
    public static bool IsNotSung(string line)
    {
        var text = line.Trim();
        return text.Equals("All rights reserved", StringComparison.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"^CH\s*\d{1,2}\s*:", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>UTF-8 when the bytes are valid UTF-8 with non-ASCII characters, else Windows-1252.</summary>
    private static string Decode(byte[] data)
    {
        if (data.Any(b => b >= 0x80))
        {
            try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(data); }
            catch (DecoderFallbackException) { }
        }
        return Latin.GetString(data);
    }

    /// <summary>Ticks to seconds, following the tempo changes.</summary>
    private sealed class TempoMap
    {
        private readonly int _division;
        private readonly List<(long Tick, int Tempo, double Seconds)> _segments = new();

        public TempoMap(int division, List<(long Tick, int Tempo)> tempos)
        {
            _division = Math.Max(1, division);
            long tick = 0;
            var tempo = 500000; // 120 bpm until the first tempo event
            double seconds = 0;
            _segments.Add((0, tempo, 0));
            foreach (var (t, us) in tempos.OrderBy(x => x.Tick))
            {
                if (us <= 0) continue;
                seconds += (t - tick) * (double)tempo / _division / 1e6;
                tick = t;
                tempo = us;
                _segments.Add((tick, tempo, seconds));
            }
        }

        public double Seconds(long tick)
        {
            var s = _segments[0];
            foreach (var seg in _segments)
            {
                if (seg.Tick > tick) break;
                s = seg;
            }
            return s.Seconds + (tick - s.Tick) * (double)s.Tempo / _division / 1e6;
        }
    }

    private sealed class ChunkReader
    {
        private readonly byte[] _data;
        public ChunkReader(byte[] data) => _data = data;
        public int Position { get; set; }
        public int Remaining => _data.Length - Position;

        public byte PeekByte() => _data[Position];
        public byte ReadByte() => _data[Position++];

        public byte[] ReadBytes(int count)
        {
            count = Math.Max(0, Math.Min(count, Remaining));
            var result = _data.AsSpan(Position, count).ToArray();
            Position += count;
            return result;
        }

        public string ReadId() => Encoding.ASCII.GetString(ReadBytes(4));
        public ushort ReadUInt16() => (ushort)((ReadByte() << 8) | ReadByte());
        public uint ReadUInt32() => ((uint)ReadByte() << 24) | ((uint)ReadByte() << 16) | ((uint)ReadByte() << 8) | ReadByte();

        public long ReadVarLength()
        {
            long value = 0;
            for (var i = 0; i < 4; i++)
            {
                var b = ReadByte();
                value = (value << 7) | (uint)(b & 0x7F);
                if (b < 0x80) break;
            }
            return value;
        }
    }
}
