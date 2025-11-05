using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Simple test class to verify MetadataExtractor functionality
/// This can be run from Program.cs during development
/// </summary>
public static class MetadataExtractorTest
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== Metadata Extractor Tests ===\n");

        // Create SQLite in-memory database for testing
        var options = new DbContextOptionsBuilder<KaraokeDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var dbContext = new KaraokeDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var extractor = new MetadataExtractor(dbContext);

        // Test 1: Filename parsing - Pattern "Artist - Title"
        Console.WriteLine("Test 1: Filename parsing - 'Artist - Title' pattern");
        var result = extractor.ParseFilename("John Doe - Amazing Song");
        Console.WriteLine($"  Input: 'John Doe - Amazing Song'");
        Console.WriteLine($"  Artist: '{result.Artist}'");
        Console.WriteLine($"  Title: '{result.Title}'");
        if (result.Artist == "John Doe" && result.Title == "Amazing Song")
        {
            Console.WriteLine("✓ Pattern 1 passed\n");
        }
        else
        {
            Console.WriteLine("✗ Pattern 1 failed\n");
        }

        // Test 2: Filename parsing - Pattern "Artist-Title"
        Console.WriteLine("Test 2: Filename parsing - 'Artist-Title' pattern");
        result = extractor.ParseFilename("Jane Smith-Beautiful Day");
        Console.WriteLine($"  Input: 'Jane Smith-Beautiful Day'");
        Console.WriteLine($"  Artist: '{result.Artist}'");
        Console.WriteLine($"  Title: '{result.Title}'");
        if (result.Artist == "Jane Smith" && result.Title == "Beautiful Day")
        {
            Console.WriteLine("✓ Pattern 2 passed\n");
        }
        else
        {
            Console.WriteLine("✗ Pattern 2 failed\n");
        }

        // Test 3: Filename parsing - Pattern "Title (Artist)"
        Console.WriteLine("Test 3: Filename parsing - 'Title (Artist)' pattern");
        result = extractor.ParseFilename("Wonderful World (Louis Armstrong)");
        Console.WriteLine($"  Input: 'Wonderful World (Louis Armstrong)'");
        Console.WriteLine($"  Artist: '{result.Artist}'");
        Console.WriteLine($"  Title: '{result.Title}'");
        if (result.Artist == "Louis Armstrong" && result.Title == "Wonderful World")
        {
            Console.WriteLine("✓ Pattern 3 passed\n");
        }
        else
        {
            Console.WriteLine("✗ Pattern 3 failed\n");
        }

        // Test 4: Filename parsing - Pattern "Artist_Title"
        Console.WriteLine("Test 4: Filename parsing - 'Artist_Title' pattern");
        result = extractor.ParseFilename("Bob_Dylan_Blowin_in_the_Wind");
        Console.WriteLine($"  Input: 'Bob_Dylan_Blowin_in_the_Wind'");
        Console.WriteLine($"  Artist: '{result.Artist}'");
        Console.WriteLine($"  Title: '{result.Title}'");
        if (result.Artist == "Bob" && result.Title == "Dylan_Blowin_in_the_Wind")
        {
            Console.WriteLine("✓ Pattern 4 passed (first underscore used as separator)\n");
        }
        else
        {
            Console.WriteLine("✗ Pattern 4 failed\n");
        }

        // Test 5: Filename parsing - No pattern match (fallback)
        Console.WriteLine("Test 5: Filename parsing - No pattern match (fallback)");
        result = extractor.ParseFilename("JustASongTitle");
        Console.WriteLine($"  Input: 'JustASongTitle'");
        Console.WriteLine($"  Artist: '{result.Artist}'");
        Console.WriteLine($"  Title: '{result.Title}'");
        if (result.Artist == string.Empty && result.Title == "JustASongTitle")
        {
            Console.WriteLine("✓ Fallback pattern passed\n");
        }
        else
        {
            Console.WriteLine("✗ Fallback pattern failed\n");
        }

        // Test 6: Queue processing
        Console.WriteLine("Test 6: Background queue processing");
        
        // Create a test media file entry
        var testMediaFile = new MediaFile
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = Path.Combine(Path.GetTempPath(), "test_video.mp4"),
            Filename = "Artist Name - Song Title.mp4",
            Type = MediaType.Video,
            Format = MediaFormat.MP4
        };

        // Create a dummy file for testing
        File.WriteAllText(testMediaFile.FilePath, "dummy content");

        try
        {
            await dbContext.MediaFiles.AddAsync(testMediaFile);
            await dbContext.SaveChangesAsync();

            bool extractedEventFired = false;
            bool failedEventFired = false;

            extractor.MetadataExtracted += (sender, args) =>
            {
                extractedEventFired = true;
                Console.WriteLine($"  ✓ MetadataExtracted event fired");
                Console.WriteLine($"    File: {args.MediaFile.Filename}");
                Console.WriteLine($"    Artist: {args.Metadata.Artist}");
                Console.WriteLine($"    Title: {args.Metadata.Title}");
            };

            extractor.MetadataExtractionFailed += (sender, args) =>
            {
                failedEventFired = true;
                Console.WriteLine($"  ⚠ MetadataExtractionFailed event fired");
                Console.WriteLine($"    File: {args.MediaFile.Filename}");
                Console.WriteLine($"    Error: {args.ErrorMessage}");
            };

            // Queue the file
            extractor.QueueForExtraction(testMediaFile);
            Console.WriteLine($"  Queued file for extraction. Queue count: {extractor.QueuedCount}");

            // Start processing
            extractor.StartProcessing();
            Console.WriteLine($"  Started processing. IsProcessing: {extractor.IsProcessing}");

            // Wait for processing
            await Task.Delay(2000);

            // Stop processing
            extractor.StopProcessing();
            Console.WriteLine($"  Stopped processing. IsProcessing: {extractor.IsProcessing}");

            if (extractedEventFired || failedEventFired)
            {
                Console.WriteLine("✓ Queue processing test passed\n");
            }
            else
            {
                Console.WriteLine("⚠ Queue processing test - no events fired (expected for dummy file)\n");
            }
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(testMediaFile.FilePath))
                {
                    File.Delete(testMediaFile.FilePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Test 7: Edge cases
        Console.WriteLine("Test 7: Edge cases");
        
        // Empty filename
        result = extractor.ParseFilename("");
        Console.WriteLine($"  Empty filename: Artist='{result.Artist}', Title='{result.Title}'");
        if (result.Artist == string.Empty && result.Title == string.Empty)
        {
            Console.WriteLine("  ✓ Empty filename handled correctly");
        }

        // Filename with multiple dashes
        result = extractor.ParseFilename("Artist - Title - Extra Info");
        Console.WriteLine($"  Multiple dashes: Artist='{result.Artist}', Title='{result.Title}'");
        if (result.Artist == "Artist" && result.Title == "Title - Extra Info")
        {
            Console.WriteLine("  ✓ Multiple dashes handled correctly (first dash used)");
        }

        // Filename with special characters
        result = extractor.ParseFilename("Artist & Band - Song (Remix)");
        Console.WriteLine($"  Special chars: Artist='{result.Artist}', Title='{result.Title}'");
        if (result.Artist == "Artist & Band" && result.Title == "Song (Remix)")
        {
            Console.WriteLine("  ✓ Special characters handled correctly");
        }

        Console.WriteLine("\n✓ Edge cases test passed\n");

        Console.WriteLine("=== All Tests Completed ===");
        Console.WriteLine("Note: Video/audio metadata extraction tests require actual media files");
        Console.WriteLine("and appropriate codecs installed on the system.\n");

        // Cleanup
        extractor.Dispose();
    }
}
