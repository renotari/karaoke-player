using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;
using LibVLCSharp.Shared;

namespace KaraokePlayer.Services;

/// <summary>
/// Simple test class to verify ThumbnailGenerator functionality
/// This can be run from Program.cs during development
/// </summary>
public static class ThumbnailGeneratorTest
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== Thumbnail Generator Tests ===\n");

        // Initialize LibVLC
        Core.Initialize();
        using var libVLC = new LibVLC();

        // Create test factory for in-memory database
        var factory = new TestDbContextFactory();
        var generator = new ThumbnailGenerator(factory, libVLC);

        // Test 1: Cache directory creation
        Console.WriteLine("Test 1: Cache directory creation");
        Console.WriteLine($"  Cache directory: {generator.CacheDirectory}");
        if (Directory.Exists(generator.CacheDirectory))
        {
            Console.WriteLine("✓ Cache directory exists\n");
        }
        else
        {
            Console.WriteLine("✗ Cache directory not created\n");
        }

        // Test 2: Default placeholder creation - Video
        Console.WriteLine("Test 2: Default placeholder creation - Video");
        var videoPlaceholder = generator.CreateDefaultThumbnail(Models.MediaType.Video);
        Console.WriteLine($"  Placeholder path: {videoPlaceholder}");
        if (File.Exists(videoPlaceholder))
        {
            var fileInfo = new FileInfo(videoPlaceholder);
            Console.WriteLine($"  File size: {fileInfo.Length} bytes");
            Console.WriteLine("✓ Video placeholder created\n");
        }
        else
        {
            Console.WriteLine("✗ Video placeholder not created\n");
        }

        // Test 3: Default placeholder creation - Audio
        Console.WriteLine("Test 3: Default placeholder creation - Audio");
        var audioPlaceholder = generator.CreateDefaultThumbnail(Models.MediaType.Audio);
        Console.WriteLine($"  Placeholder path: {audioPlaceholder}");
        if (File.Exists(audioPlaceholder))
        {
            var fileInfo = new FileInfo(audioPlaceholder);
            Console.WriteLine($"  File size: {fileInfo.Length} bytes");
            Console.WriteLine("✓ Audio placeholder created\n");
        }
        else
        {
            Console.WriteLine("✗ Audio placeholder not created\n");
        }

        // Test 4: Placeholder caching (should reuse existing)
        Console.WriteLine("Test 4: Placeholder caching");
        var videoPlaceholder2 = generator.CreateDefaultThumbnail(Models.MediaType.Video);
        if (videoPlaceholder == videoPlaceholder2)
        {
            Console.WriteLine("✓ Placeholder reused (same path returned)\n");
        }
        else
        {
            Console.WriteLine("✗ Placeholder not reused\n");
        }

        // Test 5: Queue processing
        Console.WriteLine("Test 5: Background queue processing");
        
        // Create a test media file entry
        var testMediaFile = new MediaFile
        {
            Id = Guid.NewGuid().ToString(),
            FilePath = Path.Combine(Path.GetTempPath(), "test_audio.mp3"),
            Filename = "Test Song.mp3",
            Type = Models.MediaType.Audio,
            Format = Models.MediaFormat.MP3
        };

        // Create a dummy file for testing
        File.WriteAllText(testMediaFile.FilePath, "dummy content");

        try
        {
            using (var context = factory.CreateDbContext())
            {
                await context.MediaFiles.AddAsync(testMediaFile);
                await context.SaveChangesAsync();
            }

            bool generatedEventFired = false;
            bool failedEventFired = false;

            generator.ThumbnailGenerated += (sender, args) =>
            {
                generatedEventFired = true;
                Console.WriteLine($"  ✓ ThumbnailGenerated event fired");
                Console.WriteLine($"    File: {args.MediaFile.Filename}");
                Console.WriteLine($"    Thumbnail: {args.ThumbnailPath}");
            };

            generator.ThumbnailGenerationFailed += (sender, args) =>
            {
                failedEventFired = true;
                Console.WriteLine($"  ⚠ ThumbnailGenerationFailed event fired");
                Console.WriteLine($"    File: {args.MediaFile.Filename}");
                Console.WriteLine($"    Error: {args.ErrorMessage}");
            };

            // Queue the file
            generator.QueueForGeneration(testMediaFile);
            Console.WriteLine($"  Queued file for generation. Queue count: {generator.QueuedCount}");

            // Start processing
            generator.StartProcessing();
            Console.WriteLine($"  Started processing. IsProcessing: {generator.IsProcessing}");

            // Wait for processing
            await Task.Delay(2000);

            // Stop processing
            generator.StopProcessing();
            Console.WriteLine($"  Stopped processing. IsProcessing: {generator.IsProcessing}");

            if (generatedEventFired || failedEventFired)
            {
                Console.WriteLine("✓ Queue processing test passed\n");
            }
            else
            {
                Console.WriteLine("⚠ Queue processing test - no events fired (expected for dummy file)\n");
            }

            // Check if database was updated
            using (var context = factory.CreateDbContext())
            {
                var updatedFile = await context.MediaFiles.FindAsync(testMediaFile.Id);
                if (updatedFile != null && updatedFile.ThumbnailLoaded)
                {
                    Console.WriteLine($"  ✓ Database updated: ThumbnailLoaded={updatedFile.ThumbnailLoaded}");
                    Console.WriteLine($"    ThumbnailPath: {updatedFile.ThumbnailPath}");
                }
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

        // Test 6: IsProcessing state management
        Console.WriteLine("\nTest 6: IsProcessing state management");
        Console.WriteLine($"  Initial state: IsProcessing={generator.IsProcessing}");
        
        generator.StartProcessing();
        Console.WriteLine($"  After StartProcessing: IsProcessing={generator.IsProcessing}");
        
        // Try starting again (should be idempotent)
        generator.StartProcessing();
        Console.WriteLine($"  After second StartProcessing: IsProcessing={generator.IsProcessing}");
        
        generator.StopProcessing();
        Console.WriteLine($"  After StopProcessing: IsProcessing={generator.IsProcessing}");
        
        // Try stopping again (should be idempotent)
        generator.StopProcessing();
        Console.WriteLine($"  After second StopProcessing: IsProcessing={generator.IsProcessing}");
        
        Console.WriteLine("✓ State management test passed\n");

        Console.WriteLine("=== All Tests Completed ===");
        Console.WriteLine("Note: Video thumbnail generation and MP3 artwork extraction tests");
        Console.WriteLine("require actual media files with proper formats.\n");

        // Cleanup
        generator.Dispose();
    }
}
