using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Test class for CacheManager
/// </summary>
public class CacheManagerTest
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== CacheManager Tests ===\n");

        var tempDir = Path.Combine(Path.GetTempPath(), "karaoke-cache-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            await TestBasicSetAndGet(tempDir);
            await TestInvalidation(tempDir);
            await TestLRUEviction(tempDir);
            await TestCacheStats(tempDir);
            await TestPersistence(tempDir);
            await TestMediaLibraryIntegration(tempDir);

            Console.WriteLine("\n✓ All CacheManager tests passed!");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                try
                {
                    // Wait a moment for database connections to fully close
                    await Task.Delay(100);
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors (file might be locked by SQLite)
                }
            }
        }
    }

    private static async Task TestBasicSetAndGet(string cacheDir)
    {
        Console.WriteLine("Test: Basic Set and Get");

        using var cache = new CacheManager(cacheDir);

        // Test metadata caching
        var metadata = new MediaMetadata
        {
            Artist = "Test Artist",
            Title = "Test Song",
            Duration = 180
        };

        cache.Set("file1", metadata, CacheCategory.Metadata);
        var retrieved = cache.Get<MediaMetadata>("file1", CacheCategory.Metadata);

        if (retrieved == null || retrieved.Artist != "Test Artist")
        {
            throw new Exception("Failed to retrieve cached metadata");
        }

        // Test thumbnail caching
        var thumbnailData = "base64-encoded-thumbnail-data";
        cache.Set("file1", thumbnailData, CacheCategory.Thumbnail);
        var retrievedThumb = cache.Get<string>("file1", CacheCategory.Thumbnail);

        if (retrievedThumb != thumbnailData)
        {
            throw new Exception("Failed to retrieve cached thumbnail");
        }

        // Test cache miss
        var missing = cache.Get<string>("nonexistent", CacheCategory.Metadata);
        if (missing != null)
        {
            throw new Exception("Should return null for cache miss");
        }

        Console.WriteLine("  ✓ Basic set and get working correctly\n");
        await Task.CompletedTask;
    }

    private static async Task TestInvalidation(string cacheDir)
    {
        Console.WriteLine("Test: Cache Invalidation");

        using var cache = new CacheManager(cacheDir);

        // Add some entries
        cache.Set("file1", "data1", CacheCategory.Metadata);
        cache.Set("file2", "data2", CacheCategory.Metadata);
        cache.Set("file3", "thumb3", CacheCategory.Thumbnail);

        // Test single invalidation
        cache.Invalidate("file1", CacheCategory.Metadata);
        var retrieved = cache.Get<string>("file1", CacheCategory.Metadata);
        if (retrieved != null)
        {
            throw new Exception("Invalidated entry should not be retrievable");
        }

        // Verify other entries still exist
        var file2 = cache.Get<string>("file2", CacheCategory.Metadata);
        if (file2 != "data2")
        {
            throw new Exception("Other entries should remain after single invalidation");
        }

        // Test category invalidation
        cache.InvalidateAll(CacheCategory.Metadata);
        var file2After = cache.Get<string>("file2", CacheCategory.Metadata);
        if (file2After != null)
        {
            throw new Exception("All metadata entries should be invalidated");
        }

        // Verify other categories still exist
        var thumb3 = cache.Get<string>("file3", CacheCategory.Thumbnail);
        if (thumb3 != "thumb3")
        {
            throw new Exception("Other categories should remain after category invalidation");
        }

        // Test clear all
        cache.Clear();
        var thumb3After = cache.Get<string>("file3", CacheCategory.Thumbnail);
        if (thumb3After != null)
        {
            throw new Exception("All entries should be cleared");
        }

        Console.WriteLine("  ✓ Cache invalidation working correctly\n");
        await Task.CompletedTask;
    }

    private static async Task TestLRUEviction(string cacheDir)
    {
        Console.WriteLine("Test: LRU Eviction Policy");

        // Create cache with 2MB size limit (strings are UTF-16, so 400K chars = 800KB estimated)
        using var cache = new CacheManager(cacheDir, maxThumbnailCacheSizeMB: 2);

        // Add thumbnails that exceed the limit
        var largeData1 = new string('A', 400000); // 800KB estimated (400K chars × 2 bytes/char)
        var largeData2 = new string('B', 400000); // 800KB estimated
        var largeData3 = new string('C', 400000); // 800KB estimated

        cache.Set("thumb1", largeData1, CacheCategory.Thumbnail);
        cache.Set("thumb2", largeData2, CacheCategory.Thumbnail);

        // Access thumb1 to make it more recently used
        var _ = cache.Get<string>("thumb1", CacheCategory.Thumbnail);

        // Add thumb3, which should evict thumb2 (least recently used)
        cache.Set("thumb3", largeData3, CacheCategory.Thumbnail);

        var stats = cache.GetCacheStats();
        
        // thumb2 should be evicted
        var thumb2 = cache.Get<string>("thumb2", CacheCategory.Thumbnail);
        if (thumb2 != null)
        {
            throw new Exception("LRU entry should have been evicted");
        }

        // thumb1 and thumb3 should still exist
        var thumb1 = cache.Get<string>("thumb1", CacheCategory.Thumbnail);
        var thumb3 = cache.Get<string>("thumb3", CacheCategory.Thumbnail);
        
        if (thumb1 == null || thumb3 == null)
        {
            throw new Exception("Recently used entries should not be evicted");
        }

        Console.WriteLine("  ✓ LRU eviction working correctly\n");
        await Task.CompletedTask;
    }

    private static async Task TestCacheStats(string cacheDir)
    {
        Console.WriteLine("Test: Cache Statistics");

        using var cache = new CacheManager(cacheDir);

        // Add various entries
        cache.Set("file1", "metadata1", CacheCategory.Metadata);
        cache.Set("file2", "metadata2", CacheCategory.Metadata);
        cache.Set("file3", "thumbnail3", CacheCategory.Thumbnail);
        cache.Set("search1", "results", CacheCategory.Search);

        // Perform some gets to track hits/misses
        cache.Get<string>("file1", CacheCategory.Metadata); // Hit
        cache.Get<string>("nonexistent", CacheCategory.Metadata); // Miss
        cache.Get<string>("file3", CacheCategory.Thumbnail); // Hit

        var stats = cache.GetCacheStats();

        if (stats.TotalEntries != 4)
        {
            throw new Exception($"Expected 4 total entries, got {stats.TotalEntries}");
        }

        if (stats.MetadataCount != 2)
        {
            throw new Exception($"Expected 2 metadata entries, got {stats.MetadataCount}");
        }

        if (stats.ThumbnailCount != 1)
        {
            throw new Exception($"Expected 1 thumbnail entry, got {stats.ThumbnailCount}");
        }

        if (stats.SearchCount != 1)
        {
            throw new Exception($"Expected 1 search entry, got {stats.SearchCount}");
        }

        if (stats.Hits != 2)
        {
            throw new Exception($"Expected 2 hits, got {stats.Hits}");
        }

        if (stats.Misses != 1)
        {
            throw new Exception($"Expected 1 miss, got {stats.Misses}");
        }

        var expectedHitRate = (2.0 / 3.0) * 100;
        if (Math.Abs(stats.HitRate - expectedHitRate) > 0.1)
        {
            throw new Exception($"Expected hit rate ~{expectedHitRate:F1}%, got {stats.HitRate:F1}%");
        }

        Console.WriteLine($"  ✓ Cache stats: {stats.TotalEntries} entries, {stats.HitRate:F1}% hit rate\n");
        await Task.CompletedTask;
    }

    private static async Task TestPersistence(string cacheDir)
    {
        Console.WriteLine("Test: Cache Persistence");

        // Create cache and add entries
        using (var cache = new CacheManager(cacheDir))
        {
            cache.Set("file1", "metadata1", CacheCategory.Metadata);
            cache.Set("file2", "thumbnail2", CacheCategory.Thumbnail);
            
            await cache.SaveCacheIndexAsync();
        }

        // Verify index file was created
        var indexPath = Path.Combine(cacheDir, "cache-index.json");
        if (!File.Exists(indexPath))
        {
            throw new Exception("Cache index file was not created");
        }

        // Create new cache instance and load index
        using (var cache = new CacheManager(cacheDir))
        {
            await cache.LoadCacheIndexAsync();
            
            // Note: The current implementation only loads metadata, not actual values
            // This is intentional for fast startup
            Console.WriteLine("  ✓ Cache index persisted and loaded successfully\n");
        }

        await Task.CompletedTask;
    }

    private static async Task TestMediaLibraryIntegration(string cacheDir)
    {
        Console.WriteLine("Test: Media Library Integration");

        var factory = new TestDbContextFactory();
        using var mediaLibrary = new MediaLibraryManager(factory);
        using var cache = new CacheManager(cacheDir);

        // Subscribe to media library events
        cache.SubscribeToMediaLibraryEvents(mediaLibrary);

        // Add cache entries
        cache.Set("file1", "metadata1", CacheCategory.Metadata);
        cache.Set("file1", "thumbnail1", CacheCategory.Thumbnail);
        cache.Set("query1", "results", CacheCategory.Search);

        // Simulate file modification event
        var testFile = new MediaFile
        {
            Id = "file1",
            FilePath = "/test/file1.mp4",
            Filename = "file1.mp4",
            Type = MediaType.Video,
            Format = MediaFormat.MP4
        };

        var eventArgs = new MediaFilesChangedEventArgs
        {
            Files = new System.Collections.Generic.List<MediaFile> { testFile }
        };

        // Trigger the event manually (simulating file change)
        mediaLibrary.GetType()
            .GetMethod("OnFilesModified", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(mediaLibrary, new object[] { eventArgs });

        // Give event handlers time to process
        await Task.Delay(100);

        // Verify cache was invalidated
        var metadata = cache.Get<string>("file1", CacheCategory.Metadata);
        var thumbnail = cache.Get<string>("file1", CacheCategory.Thumbnail);
        var search = cache.Get<string>("query1", CacheCategory.Search);

        if (metadata != null || thumbnail != null)
        {
            throw new Exception("File-specific cache should be invalidated on file change");
        }

        if (search != null)
        {
            throw new Exception("Search cache should be invalidated on file change");
        }

        Console.WriteLine("  ✓ Media library integration working correctly\n");
    }
}
