using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>Popular songs list: CSV reading and matching with messy library names.</summary>
public static class PopularSongsTest
{
    private static void Check(bool condition, string what)
    {
        if (condition) Console.WriteLine($"  ✓ {what}");
        else { TestFailures.Fail($"  ✗ {what}"); throw new Exception(what); }
    }

    private static MediaFile File(string name, MediaType type = MediaType.Video) => new()
    {
        Id = Guid.NewGuid().ToString(),
        FilePath = "/library/" + name,
        Filename = name,
        Type = type
    };

    public static Task RunTests()
    {
        const string csv = "rank,artist,title,language,category,score,sources\n" +
            "1,Toto Cutugno,L'italiano,it,,1.35,KaraFun Top Italia #64; Singa italiano #3\n" +
            "2,Umberto Tozzi,Gloria,it,,1.47,x\n" +
            "3,Gloria Gaynor,I Will Survive,en,,0.98,x\n" +
            "4,ABBA,Mamma Mia,en,,0.9,x\n" +
            "5,Domenico Modugno,Volare (Nel blu dipinto di blu),it,,0.8,x\n" +
            "6,Traditional,Happy Birthday,en,,0.5,x\n" +
            "7,Tiziano Ferro,Sere nere,it,,1.58,x\n" +
            "8,Gianni Morandi,Parla più piano,it,,0.8,x\n" +
            "9,Wolfgang Amadeus Mozart (Don Giovanni),\"Madamina, il catalogo è questo\",it,lirica,0.1,x\n";
        var songs = PopularSongs.Parse(new StringReader(csv));
        Check(songs.Count == 9 && songs[8].Title == "Madamina, il catalogo è questo" && songs[8].IsOpera,
            "CSV with a quoted comma, opera category");
        Check(songs[0].IsItalian && !songs[2].IsItalian, "language");

        var library = new[]
        {
            File("LItaliano-TotoCutugno.kar", MediaType.Midi),                  // 0
            File("Toto Cutugno - L'Italiano (Karaoke) [AbCdEfGhIjK].mp4"),     // 1
            File("Gloria Gaynor - I Will Survive.mp4"),                         // 2
            File("Ricchi e Poveri - Mamma Maria.mp3", MediaType.Audio),         // 3
            File("Domenico Modugno - Nel blu dipinto di blu.mp4"),              // 4
            File("Happy Birthday.mp3", MediaType.Audio),                        // 5
            File("Happy Birthday Remix - DJ Nobody.mp3", MediaType.Audio),      // 6
            File("Sere nere.mp3", MediaType.Audio),                             // 7
            File("Patrizio Buanne - Parla Piu Piano.mp4"),                      // 8
            File("GIANNI MORANDI - PARLA PIÙ PIANO.mp3", MediaType.Audio)       // 9
        };
        var m = PopularSongs.Match(songs, library);
        bool Has(int song, params int[] files) => m[song].Files.SequenceEqual(files.Select(i => library[i]));

        Check(Has(0, 1, 0), "\"LItaliano-TotoCutugno.kar\" found; the video comes before the MIDI file");
        Check(!m[1].InLibrary, "\"Gloria\" is not found in \"Gloria Gaynor - I Will Survive\"");
        Check(Has(2, 2), "Gloria Gaynor's song found");
        Check(!m[3].InLibrary, "\"Mamma Mia\" is not \"Mamma Maria\"");
        Check(Has(4, 4), "title in brackets: \"Nel blu dipinto di blu\" is Volare");
        Check(Has(5, 5), "traditional song: exact title only (not the remix)");
        Check(Has(6, 7), "file without artist: exact long title");
        Check(Has(7, 9), "same title, other artist not taken (Buanne vs Morandi); case and accents ignored");

        // A MIDI with the exact title does not come before a video with a messier name
        var both = new[] { File("Toto Cutugno - L'Italiano.kar", MediaType.Midi), File("LItaliano-TotoCutugno.mp4") };
        var order = PopularSongs.Match(songs, both)[0].Files;
        Check(order.Count == 2 && order[0] == both[1],
            "video before MIDI even when only the MIDI has the exact title (Codex review 86c3b67, P2)");

        // The list shipped with the app
        var shipped = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "karaoke-popular-songs.csv");
        if (!System.IO.File.Exists(shipped)) shipped = Path.Combine("Assets", "karaoke-popular-songs.csv");
        if (System.IO.File.Exists(shipped))
        {
            using var reader = new StreamReader(shipped);
            var list = PopularSongs.Parse(reader);
            Check(list.Count == 255 && list[0].Rank == 1 && list.All(s => s.Artist.Length > 0 && s.Title.Length > 0),
                $"shipped list: {list.Count} songs, all with artist and title");
        }
        else
        {
            Console.WriteLine("  - shipped list not found from the test folder: not checked");
        }
        return Task.CompletedTask;
    }
}
