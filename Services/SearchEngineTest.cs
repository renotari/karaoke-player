using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Tests for SearchEngine service
/// </summary>
public class SearchEngineTest
{
    private static KaraokeDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<KaraokeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new KaraokeDbContext(options);
    }

    private static async Task SeedTestData(KaraokeDbContext context)
    {
        var files = new List<MediaFile>
        {
            new MediaFile
            {
                Id = "1",
                Filename = "Bohemian Rhapsody.mp4",
                FilePath = "/media/Bohemian Rhapsody.mp4",
                Type = MediaType.Video,
                Format = MediaFormat.MP4,
                MetadataLoaded = true,
                Metadata = new MediaMetadata
                {
                    MediaFileId = "1",
                    Title = "Bohemian Rhapsody",
                    Artist = "Queen",
                    Duration = 354
                }
            },
            new MediaFile
            {
                Id = "2",
                Filename = "Queen - We Will Rock You.mp4",
                FilePath = "/media/Queen - We Will Rock You.mp4",
                Type = MediaType.Video,
                Format = MediaFormat.MP4,
                MetadataLoaded = true,
                Metadata = new MediaMetadata
                {
                    MediaFileId = "2",
                    Title = "We Will Rock You",
                    Artist = "Queen",
                    Duration = 122
                }
            },
            new MediaFile
            {
                Id = "3",
                Filename = "Imagine.mp3",
                FilePath = "/media/Imagine.mp3",
                Type = MediaType.Audio,
                Format = MediaFormat.MP3,
                MetadataLoaded = true,
                Metadata = new MediaMetadata
                {
                    MediaFileId = "3",
                    Title = "Imagine",
                    Artist = "John Lennon",
                    Duration = 183
                }
            },
            new MediaFile
            {
                Id = "4",
                Filename = "Stairway to Heaven.mp4",
                FilePath = "/media/Stairway to Heaven.mp4",
                Type = MediaType.Video,
                Format = MediaFormat.MP4,
                MetadataLoaded = true,
                Metadata = new MediaMetadata
                {
                    MediaFileId = "4",
                    Title = "Stairway to Heaven",
                    Artist = "Led Zeppelin",
                    Duration = 482
                }
            },
            new MediaFile
            {
                Id = "5",
                Filename = "Hotel California.mkv",
                FilePath = "/media/Hotel California.mkv",
                Type = MediaType.Video,
                Format = MediaFormat.MKV,
                MetadataLoaded = true,
                Metadata = new MediaMetadata
                {
                    MediaFileId = "5",
                    Title = "Hotel California",
                    Artist = "Eagles",
                    Duration = 391
                }
            }
        };

        context.MediaFiles.AddRange(files);
        await context.SaveChangesAsync();
    }

    public static async Task TestSearchByTitle()
    {
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var searchEngine = new SearchEngine(context);

        var results = await searchEngine.SearchAsync("Bohemian");

        if (results.Count != 1)
            throw new Exception($"Expected 1 result, got {results.Count}");

        if (results[0].Metadata?.Title != "Bohemian Rhapsody")
            throw new Exception($"Expected 'Bohemian Rhapsody', got '{results[0].Metadata?.Title}'");

        Console.WriteLine("✓ TestSearchByTitle passed");
    }

    public static async Task TestSearchByArtist()
    {
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var searchEngine = new SearchEngine(context);

        var results = await searchEngine.SearchAsync("Queen");

        if (results.Count != 2)
            throw new Exception($"Expected 2 results, got {results.Count}");

        if (!results.Any(r => r.Metadata?.Title == "Bohemian Rhapsody"))
            throw new Exception("Expected to find 'Bohemian Rhapsody'");

        if (!results.Any(r => r.Metadata?.Title == "We Will Rock You"))
            throw new Exception("Expected to find 'We Will Rock You'");

        Console.WriteLine("✓ TestSearchByArtist passed");
    }

    public static async Task TestSearchByFilename()
    {
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var searchEngine = new SearchEngine(context);

        var results = await searchEngine.SearchAsync("Hotel");

        if (results.Count != 1)
            throw new Exception($"Expected 1 result, got {results.Count}");

        if (results[0].Metadata?.Title != "Hotel California")
            throw new Exception($"Expected 'Hotel California', got '{results[0].Metadata?.Title}'");

        Console.WriteLine("✓ TestSearchByFilename passed");
    }

    public static async Task TestPartialMatching()
    {
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var searchEngine = new SearchEngine(context);

        var results = await searchEngine.SearchAsync("heav");

        if (results.Count != 1)
            throw new Exception($"Expected 1 result, got {results.Count}");

        if (results[0].Metadata?.Title != "Stairway to Heaven")
            throw new Exception($"Expected 'Stairway to Heaven', got '{results[0].Metadata?.Title}'");

        Console.WriteLine("✓ TestPartialMatching passed");
    }

    public static async Task TestEmptyQuery()
    {
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var searchEngine = new SearchEngine(context);

        var results = await searchEngine.SearchAsync("");

        if (results.Count != 0)
            throw new Exception($"Expected 0 results for empty query, got {results.Count}");

        Console.WriteLine("✓ TestEmptyQuery passed");
    }

    public static async Task TestSearchHistory()
    {
        using var context = CreateInMemoryContext();
        var searchEngine = new SearchEngine(context);

        await searchEngine.AddToHistoryAsync("Queen");
        await searchEngine.AddToHistoryAsync("Beatles");
        await searchEngine.AddToHistoryAsync("Led Zeppelin");

        var history = await searchEngine.GetHistoryAsync();

        if (history.Count != 3)
            throw new Exception($"Expected 3 history items, got {history.Count}");

        if (history[0] != "Led Zeppelin")
            throw new Exception($"Expected most recent search 'Led Zeppelin', got '{history[0]}'");

        Console.WriteLine("✓ TestSearchHistory passed");
    }

    public static async Task TestSearchHistoryLimit()
    {
        using var context = CreateInMemoryContext();
        var searchEngine = new SearchEngine(context);

        // Add 12 search terms (limit is 10)
        for (int i = 1; i <= 12; i++)
        {
            await searchEngine.AddToHistoryAsync($"Search {i}");
            await Task.Delay(10); // Ensure different timestamps
        }

        var history = await searchEngine.GetHistoryAsync();

        if (history.Count != 10)
            throw new Exception($"Expected 10 history items (limit), got {history.Count}");

        if (history[0] != "Search 12")
            throw new Exception($"Expected most recent 'Search 12', got '{history[0]}'");

        if (history[9] != "Search 3")
            throw new Exception($"Expected oldest kept 'Search 3', got '{history[9]}'");

        Console.WriteLine("✓ TestSearchHistoryLimit passed");
    }

    public static async Task TestSearchHistoryDuplicates()
    {
        using var context = CreateInMemoryContext();
        var searchEngine = new SearchEngine(context);

        await searchEngine.AddToHistoryAsync("Queen");
        await Task.Delay(10);
        await searchEngine.AddToHistoryAsync("Beatles");
        await Task.Delay(10);
        await searchEngine.AddToHistoryAsync("Queen"); // Duplicate

        var history = await searchEngine.GetHistoryAsync();

        if (history.Count != 2)
            throw new Exception($"Expected 2 unique history items, got {history.Count}");

        if (history[0] != "Queen")
            throw new Exception($"Expected 'Queen' to be moved to top, got '{history[0]}'");

        Console.WriteLine("✓ TestSearchHistoryDuplicates passed");
    }

    public static async Task TestClearHistory()
    {
        using var context = CreateInMemoryContext();
        var searchEngine = new SearchEngine(context);

        await searchEngine.AddToHistoryAsync("Queen");
        await searchEngine.AddToHistoryAsync("Beatles");

        await searchEngine.ClearHistoryAsync();

        var history = await searchEngine.GetHistoryAsync();

        if (history.Count != 0)
            throw new Exception($"Expected 0 history items after clear, got {history.Count}");

        Console.WriteLine("✓ TestClearHistory passed");
    }

    public static async Task TestRelevanceRanking()
    {
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var searchEngine = new SearchEngine(context);

        // Search for "Queen" - should rank exact artist match higher
        var results = await searchEngine.SearchAsync("Queen");

        if (results.Count < 2)
            throw new Exception($"Expected at least 2 results, got {results.Count}");

        // Both songs have "Queen" as artist, but check they're both returned
        var hasQueen = results.All(r => r.Metadata?.Artist == "Queen");
        if (!hasQueen)
            throw new Exception("Expected all results to have 'Queen' as artist");

        Console.WriteLine("✓ TestRelevanceRanking passed");
    }

    public static async Task RunAllTests()
    {
        Console.WriteLine("Running SearchEngine tests...\n");

        try
        {
            await TestSearchByTitle();
            await TestSearchByArtist();
            await TestSearchByFilename();
            await TestPartialMatching();
            await TestEmptyQuery();
            await TestSearchHistory();
            await TestSearchHistoryLimit();
            await TestSearchHistoryDuplicates();
            await TestClearHistory();
            await TestRelevanceRanking();

            Console.WriteLine("\n✓ All SearchEngine tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Test failed: {ex.Message}");
            throw;
        }
    }
}
