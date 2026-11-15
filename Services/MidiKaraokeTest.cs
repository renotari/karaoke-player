using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KaraokePlayer.Services.Midi;

namespace KaraokePlayer.Services;

/// <summary>
/// .kar/.mid reading (lyrics, timing, the two lyric conventions), karaoke markers in
/// file names and the MIDI to WAV rendering. The MIDI files are built in memory.
/// </summary>
public static class MidiKaraokeTest
{
    private static void Check(bool condition, string what)
    {
        if (condition) Console.WriteLine($"  ✓ {what}");
        else { TestFailures.Fail($"  ✗ {what}"); throw new Exception(what); }
    }

    /// <summary>Builds a track: (delta ticks, event bytes after the delta).</summary>
    private sealed class Track
    {
        private readonly List<byte> _bytes = new();

        public Track Event(int delta, params byte[] data)
        {
            WriteVarLength(delta);
            _bytes.AddRange(data);
            return this;
        }

        public Track Meta(int delta, byte type, byte[] payload)
        {
            WriteVarLength(delta);
            _bytes.Add(0xFF);
            _bytes.Add(type);
            WriteVarLength(payload.Length);
            _bytes.AddRange(payload);
            return this;
        }

        public Track Text(int delta, string text) => Meta(delta, 0x01, Encoding.Latin1.GetBytes(text));
        public Track Lyric(int delta, string text) => Meta(delta, 0x05, Encoding.Latin1.GetBytes(text));
        public Track Tempo(int delta, int microsPerQuarter) =>
            Meta(delta, 0x51, new[] { (byte)(microsPerQuarter >> 16), (byte)(microsPerQuarter >> 8), (byte)microsPerQuarter });

        public byte[] Build()
        {
            var body = new List<byte>(_bytes);
            body.AddRange(new byte[] { 0x00, 0xFF, 0x2F, 0x00 }); // end of track
            var chunk = new List<byte>(Encoding.ASCII.GetBytes("MTrk"));
            chunk.AddRange(BigEndian((uint)body.Count));
            chunk.AddRange(body);
            return chunk.ToArray();
        }

        private void WriteVarLength(int value)
        {
            var stack = new Stack<byte>();
            stack.Push((byte)(value & 0x7F));
            while ((value >>= 7) > 0)
                stack.Push((byte)((value & 0x7F) | 0x80));
            _bytes.AddRange(stack);
        }
    }

    private static byte[] BigEndian(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    private static byte[] Midi(int division, params Track[] tracks)
    {
        var file = new List<byte>(Encoding.ASCII.GetBytes("MThd"));
        file.AddRange(BigEndian(6));
        file.AddRange(new byte[] { 0, (byte)(tracks.Length > 1 ? 1 : 0), 0, (byte)tracks.Length, (byte)(division >> 8), (byte)division });
        foreach (var t in tracks)
            file.AddRange(t.Build());
        return file.ToArray();
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.001;

    public static async Task RunTests()
    {
        // ----- Soft Karaoke: Text events, "@" header, "\" paragraph, "/" line -----
        // 48 ticks per quarter: 120 bpm (48 ticks = 0.5 s) until tick 192 (2 s), then 60 bpm (48 ticks = 1 s)
        var music = new Track()
            .Tempo(0, 500000)
            .Event(0, 0xF0, 0x05, 0x7E, 0x7F, 0x09, 0x01, 0xF7) // GM reset (SysEx) before the notes
            .Event(0, 0x90, 60, 100).Event(96, 60, 0)           // note on, off with running status
            .Tempo(96, 1000000)
            .Event(96, 0x90, 64, 100).Event(96, 0x80, 64, 0);
        var words = new Track()
            .Text(0, "@KMIDI KARAOKE FILE").Text(0, "@TCanzone").Text(0, "@TArtista")
            .Text(0, "\\All rights reserved")
            .Text(48, "\\Per").Text(48, "ché ")    // 0.5 s, 1.0 s
            .Text(0, "/cant").Text(48, "oè")   // 1.0 s, 1.5 s
            .Text(144, "\\Fi").Text(96, "ne");      // tick 288 = 2 s + 96 ticks at 60 bpm = 4 s; tick 384 = 6 s
        // Pad with syllables so the file counts as having lyrics
        for (var i = 0; i < 6; i++) words.Text(0, i == 0 ? "/la " : "la ");

        var kar = MidiKaraokeFile.Parse(Midi(48, music, words));
        var lines = kar.Lyrics.Lines;
        Check(kar.Lyrics.HasLyrics, "Soft Karaoke lyrics found");
        Check(kar.TitleLines.SequenceEqual(new[] { "Canzone", "Artista" }), "@T lines read as title and artist, not sung");
        Check(lines[0].Text == "Perché " && lines[1].Text == "cantoè", $"lines split at \\ and / ({lines[0].Text}|{lines[1].Text})");
        Check(!lines.Any(l => l.Text.Contains("rights")), "copyright line dropped");
        Check(Near(lines[0].Start, 0.5) && Near(lines[0].Syllables[1].Time, 1.0) && Near(lines[1].Syllables[1].Time, 1.5),
            "times at 120 bpm");
        Check(Near(lines[2].Start, 4.0) && Near(lines[2].Syllables[1].Time, 6.0), "times follow the tempo change (SysEx skipped correctly)");
        Check(lines[2].StartsParagraph && !lines[1].StartsParagraph, "paragraph marks");
        Check(kar.Lyrics.LineAt(0.1) == -1 && kar.Lyrics.LineAt(1.2) == 1 && kar.Lyrics.LineAt(99) == lines.Count - 1, "line at a time");
        Check(Near(kar.Duration, 6.0), $"duration from the last event ({kar.Duration:0.###} s)");

        // ----- Lyric events with CR line ends; Text events are chords -----
        var single = new Track().Tempo(0, 500000)
            .Lyric(0, "CH1: Text/Setup").Lyric(0, "\r")
            .Text(10, "C# m")
            .Lyric(86, "Uno/due ").Lyric(48, "tre").Lyric(0, "\r");
        for (var i = 0; i < 8; i++) single.Lyric(48, "si ");
        single.Lyric(0, "\r");
        var lyricFile = MidiKaraokeFile.Parse(Midi(96, single));
        var l2 = lyricFile.Lyrics.Lines;
        Check(l2[0].Text == "Uno/due tre", $"CR files keep \"/\" as text ({l2[0].Text})");
        Check(!l2.Any(l => l.Text.Contains("CH1") || l.Text.Contains("C# m")), "channel list and chords are not lyrics");
        Check(Near(l2[0].Start, 0.5) && l2.Count == 2, "lyric events timed, one line per CR");

        // ----- Every syllable followed by its line end in the same event -----
        var crEnded = new Track().Tempo(0, 500000);
        for (var i = 0; i < 12; i++) crEnded.Lyric(48, $"Riga {i}\r");
        var crFile = MidiKaraokeFile.Parse(Midi(96, crEnded));
        Check(crFile.Lyrics.Lines.Count == 12 && crFile.Lyrics.Lines[3].Text == "Riga 3",
            "text ending with CR in the same event counts as lyrics (Codex review 9c9231d, P2 #1)");

        // ----- Plain MIDI: a marker or two is not lyrics -----
        var plain = MidiKaraokeFile.Parse(Midi(96, new Track().Text(0, "stop").Event(0, 0x90, 60, 100).Event(960, 0x80, 60, 0)));
        Check(!plain.Lyrics.HasLyrics && Near(plain.Duration, 5.0), "MIDI without lyrics");

        // ----- Damaged files -----
        var truncated = Midi(48, music, words);
        var cut = MidiKaraokeFile.Parse(truncated.Take(truncated.Length - 12).ToArray());
        Check(cut.Lyrics.Lines.Count > 0, "truncated file: what was read is kept");
        var notMidi = false;
        try { MidiKaraokeFile.Parse(Encoding.ASCII.GetBytes("RIFF0000WAVE")); } catch (InvalidDataException) { notMidi = true; }
        Check(notMidi, "a file that is not MIDI is rejected");

        // ----- Karaoke markers in file names -----
        Check(FilenameParser.Parse("Me cago en el amor (k)") == ("", "Me cago en el amor"), "\"(k)\" is not the artist");
        Check(FilenameParser.Parse("Amore (kar)") == ("", "Amore"), "\"(kar)\" is not the artist");
        Check(FilenameParser.Parse("Volare (Karaoke)") == ("", "Volare"), "\"(Karaoke)\" is not the artist");
        Check(FilenameParser.Parse("Artist - Song (Karaoke Version)") == ("Artist", "Song (Karaoke Version)"),
            "with a dash \"(Karaoke Version)\" stays (original and karaoke versions)");
        Check(FilenameParser.Parse("(k)") == ("", "(k)"), "a name that is only the marker stays");

        // ----- Rendering to WAV (needs the SoundFont copied next to the app) -----
        if (MidiRenderer.FindSoundFont() == null)
        {
            Console.WriteLine("  - SoundFont not found: rendering not tested");
            return;
        }

        var dir = Path.Combine(Path.GetTempPath(), "midi-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var midiPath = Path.Combine(dir, "song.kar");
            File.WriteAllBytes(midiPath, Midi(48, music, words));
            var renderer = new MidiRenderer(Path.Combine(dir, "cache"));
            var both = await Task.WhenAll(renderer.GetAudioFileAsync(midiPath), renderer.GetAudioFileAsync(midiPath));
            Check(both[0] == both[1] && Directory.GetFiles(Path.Combine(dir, "cache")).Length == 1,
                "play and preload at the same time render once");

            var wav = File.ReadAllBytes(both[0]);
            var dataBytes = BitConverter.ToInt32(wav, 40);
            Check(Encoding.ASCII.GetString(wav, 0, 4) == "RIFF" && dataBytes == wav.Length - 44, "valid WAV header");
            var seconds = dataBytes / 4.0 / MidiRenderer.SampleRate;
            Check(seconds > 5.9 && seconds < 9, $"length: the song plus the tail ({seconds:0.0} s)");
            Check(wav.Skip(44).Any(b => b != 0), "not silent");

            var again = await renderer.GetAudioFileAsync(midiPath);
            Check(again == both[0], "second request uses the cache");

            File.SetLastWriteTimeUtc(midiPath, DateTime.UtcNow.AddMinutes(1));
            var changed = await renderer.GetAudioFileAsync(midiPath);
            Check(changed != both[0], "a modified file is rendered again");

            // A SoundFont put in the data folder wins; replacing it (same name) renders again
            var userFonts = Path.Combine(AppPaths.Root, "SoundFonts");
            Directory.CreateDirectory(userFonts);
            var userFont = Path.Combine(userFonts, "GeneralUser-GS.sf2");
            File.Copy(MidiRenderer.FindSoundFont()!, userFont, overwrite: true);
            try
            {
                Check(MidiRenderer.FindSoundFont() == userFont, "the user's SoundFont is preferred");
                var withUserFont = await renderer.GetAudioFileAsync(midiPath);
                File.SetLastWriteTimeUtc(userFont, DateTime.UtcNow.AddMinutes(5));
                var replaced = await renderer.GetAudioFileAsync(midiPath);
                Check(withUserFont != changed && replaced != withUserFont,
                    "another or replaced SoundFont renders again (Codex review 9c9231d, P2 #2)");
            }
            finally
            {
                File.Delete(userFont);
            }
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
