using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>A song of the popular karaoke list (Assets/karaoke-popular-songs.csv).</summary>
public sealed record PopularSong(int Rank, string Artist, string Title, string Language, string Category, string Sources)
{
    public bool IsItalian => Language.StartsWith("it", StringComparison.OrdinalIgnoreCase);
    public bool IsOpera => Category.Equals("lirica", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A popular song and the library files that are that song (best first; empty: missing).</summary>
public sealed record PopularSongMatch(PopularSong Song, IReadOnlyList<MediaFile> Files)
{
    public bool InLibrary => Files.Count > 0;
}

/// <summary>
/// Reads the popular songs list and finds them in the library. Library names are
/// messy ("LItaliano-TotoCutugno.kar", "Toto Cutugno - L'Italiano (Karaoke) [id].mp4"),
/// so names are compared without spaces, accents and punctuation:
/// - the title must be the library title, or (5+ letters) be contained in the file name;
///   a title in brackets counts too ("Volare (Nel blu dipinto di blu)");
/// - one word of the artist must be in the file name or the library artist, except
///   for traditional songs; a file without artist matches on the exact title (8+ letters).
/// </summary>
public static class PopularSongs
{
    private static readonly HashSet<string> NotArtistWords = new(StringComparer.Ordinal)
    {
        "the", "and", "feat", "featuring", "con", "di", "del", "dei", "les", "los", "traditional", "various", "film"
    };

    public static List<PopularSong> Parse(TextReader reader)
    {
        var songs = new List<PopularSong>();
        var header = reader.ReadLine();
        if (header == null) return songs;
        var columns = SplitCsv(header).Select(c => c.Trim().ToLowerInvariant()).ToList();
        int Col(string name) => columns.IndexOf(name);
        int rank = Col("rank"), artist = Col("artist"), title = Col("title"), language = Col("language"),
            category = Col("category"), sources = Col("sources");
        if (artist < 0 || title < 0)
            throw new InvalidDataException("The list needs 'artist' and 'title' columns");

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = SplitCsv(line);
            string Get(int i) => i >= 0 && i < f.Count ? f[i].Trim() : string.Empty;
            if (Get(title).Length == 0) continue;
            songs.Add(new PopularSong(
                int.TryParse(Get(rank), out var r) ? r : songs.Count + 1,
                Get(artist), Get(title), Get(language), Get(category), Get(sources)));
        }
        return songs;
    }

    /// <summary>One CSV line (fields in double quotes may hold commas and "" quotes).</summary>
    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        return fields;
    }

    private sealed record LibraryEntry(MediaFile File, string Name, string Title, string Artist);

    public static List<PopularSongMatch> Match(IReadOnlyList<PopularSong> songs, IEnumerable<MediaFile> library)
    {
        var entries = library.Select(f => new LibraryEntry(f,
            Compact(Path.GetFileNameWithoutExtension(f.Filename)),
            Compact(WithoutBrackets(f.DisplayTitle)),
            Compact(f.DisplayArtist))).ToList();

        var result = new List<PopularSongMatch>(songs.Count);
        foreach (var song in songs)
        {
            var titles = TitleVariants(song.Title);
            var artistWords = ArtistWords(song.Artist);
            var files = new List<(MediaFile File, int Quality)>();

            foreach (var e in entries)
            {
                var exactTitle = titles.Any(t => t == e.Title);
                var titleInName = exactTitle || titles.Any(t => t.Length >= 5 && e.Name.Contains(t, StringComparison.Ordinal));
                if (!titleInName) continue;

                bool ok;
                if (artistWords.Count == 0)
                    ok = exactTitle; // traditional song: the title alone
                else if (artistWords.Any(w => e.Name.Contains(w, StringComparison.Ordinal) || e.Artist.Contains(w, StringComparison.Ordinal)))
                    ok = true;
                else
                    ok = e.Artist.Length == 0 && exactTitle && e.Title.Length >= 8; // file without artist
                if (ok)
                    // The kind of file first (video, audio, MIDI); an exact title only breaks ties
                    files.Add((e.File, TypeQuality(e.File.Type) * 10 + (exactTitle ? 1 : 0)));
            }

            result.Add(new PopularSongMatch(song, files.OrderByDescending(x => x.Quality).Select(x => x.File).ToList()));
        }
        return result;
    }

    /// <summary>Videos first, then audio, then MIDI (the usual quality order for a karaoke night).</summary>
    private static int TypeQuality(MediaType type) => type switch
    {
        MediaType.Video => 3,
        MediaType.Audio => 2,
        _ => 1
    };

    private static List<string> TitleVariants(string title)
    {
        var variants = new List<string> { Compact(WithoutBrackets(title)) };
        foreach (Match m in Regex.Matches(title, @"\(([^)]*)\)"))
        {
            var inner = Compact(m.Groups[1].Value);
            if (inner.Length >= 5) variants.Add(inner);
        }
        return variants.Where(v => v.Length > 0).Distinct().ToList();
    }

    private static List<string> ArtistWords(string artist) =>
        Regex.Split(Normalize(artist), "[^a-z0-9]+")
            .Where(w => w.Length >= 3 && !NotArtistWords.Contains(w))
            .Distinct().ToList();

    private static string WithoutBrackets(string s) => Regex.Replace(s, @"\([^)]*\)|\[[^\]]*\]", " ");

    /// <summary>Lower case, no accents.</summary>
    private static string Normalize(string s)
    {
        var decomposed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Only letters and digits: "L'Italiano" and "LItaliano" are both "litaliano".</summary>
    public static string Compact(string s) => new(Normalize(s).Where(char.IsLetterOrDigit).ToArray());
}
