using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using Microsoft.EntityFrameworkCore;

namespace KaraokePlayer.Services;

/// <summary>
/// Tests for PlaylistManager service
/// </summary>
public class PlaylistManagerTest
{
    private KaraokeDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<KaraokeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new KaraokeDbContext(options);
    }

    private MediaFile CreateTestMediaFile(string id, string filename)
    {
        return new MediaFile
        {
            Id = id,
            FilePath = $"/test/{filename}",
            Filename = filename,
            Type = MediaType.Video,
            Format = MediaFormat.MP4,
            MetadataLoaded = true,
            ThumbnailLoaded = true,
            Metadata = new MediaMetadata
            {
                Duration = 180,
                Artist = "Test Artist",
                Title = filename.Replace(".mp4", ""),
                FileSize = 1024000
            }
        };
    }

    public async Task<bool> TestAddSongToEnd()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");

        var playlist = manager.GetCurrentPlaylist();
        
        return playlist.Count == 2 &&
               playlist[0].MediaFileId == "1" &&
               playlist[1].MediaFileId == "2";
    }

    public async Task<bool> TestAddSongNext()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");
        var mediaFile3 = CreateTestMediaFile("3", "song3.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");
        await manager.AddSongAsync(mediaFile3, "next");

        var playlist = manager.GetCurrentPlaylist();
        
        // Song 3 should be inserted at position 1 (after the first song)
        return playlist.Count == 3 &&
               playlist[0].MediaFileId == "1" &&
               playlist[1].MediaFileId == "3" &&
               playlist[2].MediaFileId == "2";
    }

    public async Task<bool> TestRemoveSong()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");
        var mediaFile3 = CreateTestMediaFile("3", "song3.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");
        await manager.AddSongAsync(mediaFile3, "end");

        await manager.RemoveSongAsync(1);

        var playlist = manager.GetCurrentPlaylist();
        
        return playlist.Count == 2 &&
               playlist[0].MediaFileId == "1" &&
               playlist[1].MediaFileId == "3";
    }

    public async Task<bool> TestReorderSong()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");
        var mediaFile3 = CreateTestMediaFile("3", "song3.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");
        await manager.AddSongAsync(mediaFile3, "end");

        await manager.ReorderSongAsync(0, 2);

        var playlist = manager.GetCurrentPlaylist();
        
        return playlist.Count == 3 &&
               playlist[0].MediaFileId == "2" &&
               playlist[1].MediaFileId == "3" &&
               playlist[2].MediaFileId == "1";
    }

    public async Task<bool> TestClearPlaylist()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");

        await manager.ClearPlaylistAsync();

        var playlist = manager.GetCurrentPlaylist();
        
        return playlist.Count == 0;
    }

    public async Task<bool> TestShufflePlaylist()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");
        var mediaFile3 = CreateTestMediaFile("3", "song3.mp4");
        var mediaFile4 = CreateTestMediaFile("4", "song4.mp4");
        var mediaFile5 = CreateTestMediaFile("5", "song5.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");
        await manager.AddSongAsync(mediaFile3, "end");
        await manager.AddSongAsync(mediaFile4, "end");
        await manager.AddSongAsync(mediaFile5, "end");

        var originalOrder = manager.GetCurrentPlaylist().Select(p => p.MediaFileId).ToList();

        await manager.ShufflePlaylistAsync();

        var shuffledOrder = manager.GetCurrentPlaylist().Select(p => p.MediaFileId).ToList();
        
        // Check that all items are still present and order changed
        return shuffledOrder.Count == 5 &&
               shuffledOrder.All(id => originalOrder.Contains(id)) &&
               !shuffledOrder.SequenceEqual(originalOrder);
    }

    public async Task<bool> TestDuplicateDetection()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        
        var isDuplicateBefore = manager.IsDuplicate(mediaFile1);
        
        await manager.AddSongAsync(mediaFile1, "end");

        var playlist = manager.GetCurrentPlaylist();
        
        return isDuplicateBefore &&
               playlist.Count == 2 &&
               playlist[1].IsDuplicate;
    }

    public async Task<bool> TestSaveAndLoadM3UPlaylist()
    {
        using var context = CreateInMemoryContext();
        
        // Add test media files to database
        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");
        
        context.MediaFiles.Add(mediaFile1);
        context.MediaFiles.Add(mediaFile2);
        await context.SaveChangesAsync();

        var manager = new PlaylistManager(context);

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");

        var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.m3u");

        try
        {
            await manager.SavePlaylistAsync(tempFile);

            var fileExists = File.Exists(tempFile);
            var content = await File.ReadAllTextAsync(tempFile);
            var hasHeader = content.Contains("#EXTM3U");
            var hasSongs = content.Contains("song1.mp4") && content.Contains("song2.mp4");

            await manager.ClearPlaylistAsync();
            await manager.LoadPlaylistAsync(tempFile);

            var playlist = manager.GetCurrentPlaylist();

            return fileExists &&
                   hasHeader &&
                   hasSongs &&
                   playlist.Count == 2 &&
                   playlist[0].MediaFileId == "1" &&
                   playlist[1].MediaFileId == "2";
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    public async Task<bool> TestPositionUpdates()
    {
        using var context = CreateInMemoryContext();
        var manager = new PlaylistManager(context);

        var mediaFile1 = CreateTestMediaFile("1", "song1.mp4");
        var mediaFile2 = CreateTestMediaFile("2", "song2.mp4");
        var mediaFile3 = CreateTestMediaFile("3", "song3.mp4");

        await manager.AddSongAsync(mediaFile1, "end");
        await manager.AddSongAsync(mediaFile2, "end");
        await manager.AddSongAsync(mediaFile3, "end");

        var playlist = manager.GetCurrentPlaylist();
        
        return playlist[0].Position == 0 &&
               playlist[1].Position == 1 &&
               playlist[2].Position == 2;
    }

    public static async Task RunAllTests()
    {
        var test = new PlaylistManagerTest();
        
        Console.WriteLine("Running PlaylistManager Tests...");
        Console.WriteLine();

        var tests = new[]
        {
            ("Add Song to End", test.TestAddSongToEnd()),
            ("Add Song Next", test.TestAddSongNext()),
            ("Remove Song", test.TestRemoveSong()),
            ("Reorder Song", test.TestReorderSong()),
            ("Clear Playlist", test.TestClearPlaylist()),
            ("Shuffle Playlist", test.TestShufflePlaylist()),
            ("Duplicate Detection", test.TestDuplicateDetection()),
            ("Save and Load M3U Playlist", test.TestSaveAndLoadM3UPlaylist()),
            ("Position Updates", test.TestPositionUpdates())
        };

        int passed = 0;
        int failed = 0;

        foreach (var (name, testTask) in tests)
        {
            try
            {
                var result = await testTask;
                if (result)
                {
                    Console.WriteLine($"✓ {name}");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"✗ {name} - Test returned false");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ {name} - {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Results: {passed} passed, {failed} failed");
    }
}
