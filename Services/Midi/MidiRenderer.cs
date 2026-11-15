using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MeltySynth;

namespace KaraokePlayer.Services.Midi;

/// <summary>
/// Turns .mid/.kar files into WAV files that LibVLC plays like any audio file
/// (so crossfade, volume, normalization and the audio device work unchanged).
/// The synthesizer is MeltySynth with a General MIDI SoundFont; renders are kept
/// in a cache folder (the most recently used ones, up to <see cref="MaxCacheBytes"/>).
/// </summary>
public sealed class MidiRenderer
{
    public const int SampleRate = 44100;
    private const long MaxCacheBytes = 2L * 1024 * 1024 * 1024; // ~45 songs of 4 minutes
    private const double TailSeconds = 2; // let the last notes ring

    private readonly string _cacheDirectory;
    private readonly ILoggingService? _logging;
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _renders = new();
    private readonly object _soundFontLock = new();
    private SoundFont? _soundFont;
    private string? _soundFontIdentity; // path, size and time of the loaded SoundFont

    public MidiRenderer(string cacheDirectory, ILoggingService? logging = null)
    {
        _cacheDirectory = cacheDirectory;
        _logging = logging;
    }

    /// <summary>
    /// The SoundFont: a .sf2 put by the user in &lt;data&gt;/SoundFonts wins over the
    /// one shipped in &lt;app&gt;/SoundFonts. Null when there is none.
    /// </summary>
    public static string? FindSoundFont()
    {
        foreach (var dir in new[]
                 {
                     Path.Combine(AppPaths.Root, "SoundFonts"),
                     Path.Combine(AppContext.BaseDirectory, "SoundFonts")
                 })
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                var found = Directory.GetFiles(dir, "*.sf2").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (found != null) return found;
            }
            catch (Exception) { /* unreadable folder: try the next one */ }
        }
        return null;
    }

    /// <summary>Path of the WAV for this MIDI file, rendering it first when needed.</summary>
    public Task<string> GetAudioFileAsync(string midiPath, CancellationToken cancellationToken = default)
    {
        var soundFont = FindSoundFont()
            ?? throw new FileNotFoundException("No SoundFont (.sf2) found: MIDI files cannot be played. Reinstall the app or put a .sf2 file in the SoundFonts folder.");

        var wav = Path.Combine(_cacheDirectory, CacheKey(midiPath, soundFont) + ".wav");
        if (File.Exists(wav))
        {
            Touch(wav);
            return Task.FromResult(wav);
        }

        // One render per file even when play and preload ask at the same time
        var lazy = _renders.GetOrAdd(wav, key => new Lazy<Task<string>>(() => Task.Run(() =>
        {
            try
            {
                Render(midiPath, soundFont, wav);
                CleanCache(keep: wav);
                return wav;
            }
            finally
            {
                _renders.TryRemove(wav, out _);
            }
        })));
        return lazy.Value.WaitAsync(cancellationToken);
    }

    private void Render(string midiPath, string soundFontPath, string wavPath)
    {
        var started = DateTime.UtcNow;
        var midi = new MidiFile(midiPath);
        var synthesizer = new Synthesizer(LoadSoundFont(soundFontPath), new SynthesizerSettings(SampleRate));
        var sequencer = new MidiFileSequencer(synthesizer);
        sequencer.Play(midi, false);

        var totalFrames = (long)((midi.Length.TotalSeconds + TailSeconds) * SampleRate);
        Directory.CreateDirectory(_cacheDirectory);
        var temp = wavPath + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        using (var writer = new BinaryWriter(stream))
        {
            WriteWavHeader(writer, totalFrames);
            var left = new float[SampleRate / 10];
            var right = new float[left.Length];
            var pcm = new byte[left.Length * 4];
            for (long done = 0; done < totalFrames; done += left.Length)
            {
                var frames = (int)Math.Min(left.Length, totalFrames - done);
                sequencer.Render(left.AsSpan(0, frames), right.AsSpan(0, frames));
                for (var i = 0; i < frames; i++)
                {
                    WriteSample(pcm, i * 4, left[i]);
                    WriteSample(pcm, i * 4 + 2, right[i]);
                }
                writer.Write(pcm, 0, frames * 4);
            }
        }

        File.Move(temp, wavPath, overwrite: true);
        _logging?.LogInformation($"MIDI rendered in {(DateTime.UtcNow - started).TotalSeconds:0.0} s: {Path.GetFileName(midiPath)}");
    }

    private SoundFont LoadSoundFont(string path)
    {
        // Loading takes a moment and the SoundFont is read-only: shared by all renders
        lock (_soundFontLock)
        {
            // Reloaded when another SoundFont is chosen or the file is replaced
            var identity = FileIdentity(path);
            if (_soundFont == null || _soundFontIdentity != identity)
            {
                _soundFont = new SoundFont(path);
                _soundFontIdentity = identity;
            }
            return _soundFont;
        }
    }

    private static void WriteSample(byte[] buffer, int offset, float value)
    {
        var s = (short)Math.Clamp((int)(value * 32767f), short.MinValue, short.MaxValue);
        buffer[offset] = (byte)s;
        buffer[offset + 1] = (byte)(s >> 8);
    }

    private static void WriteWavHeader(BinaryWriter w, long frames)
    {
        const short channels = 2, bits = 16;
        var dataBytes = (uint)(frames * channels * bits / 8);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(SampleRate);
        w.Write(SampleRate * channels * bits / 8);
        w.Write((short)(channels * bits / 8));
        w.Write(bits);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);
    }

    /// <summary>
    /// A change to the MIDI file or to the SoundFont (another file, or the same name
    /// replaced) makes a new render.
    /// </summary>
    private static string CacheKey(string midiPath, string soundFontPath)
    {
        var key = $"{FileIdentity(midiPath)}|{FileIdentity(soundFontPath)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24].ToLowerInvariant();
    }

    private static string FileIdentity(string path)
    {
        var info = new FileInfo(path);
        return $"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
    }

    private static void Touch(string path)
    {
        try { File.SetLastAccessTimeUtc(path, DateTime.UtcNow); } catch (Exception) { }
    }

    /// <summary>Deletes the least recently used renders above the size limit.</summary>
    private void CleanCache(string keep)
    {
        try
        {
            var files = new DirectoryInfo(_cacheDirectory).GetFiles("*.wav")
                .OrderByDescending(f => f.LastAccessTimeUtc).ToList();
            long total = 0;
            foreach (var f in files)
            {
                total += f.Length;
                if (total > MaxCacheBytes && f.FullName != Path.GetFullPath(keep) && !_renders.ContainsKey(f.FullName))
                    f.Delete();
            }
        }
        catch (Exception ex)
        {
            _logging?.LogWarning($"MIDI cache cleanup: {ex.Message}");
        }
    }
}
