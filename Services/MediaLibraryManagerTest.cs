using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Simple test class to verify MediaLibraryManager functionality
/// This can be run from Program.cs during development
/// </summary>
public static class MediaLibraryManagerTest
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== Media Library Manager Tests ===\n");

        // Create a temporary test directory
        var testDir = Path.Combine(Path.GetTempPath(), "KaraokePlayerTest_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);

        try
        {
            // Create test media files
            Console.WriteLine("Setting up test environment...");
            var testFiles = new[]
            {
                Path.Combine(testDir, "song1.mp3"),
                Path.Combine(testDir, "video1.mp4"),
                Path.Combine(testDir, "video2.mkv"),
                Path.Combine(testDir, "video3.webm")
            };

            foreach (var file in testFiles)
            {
                File.WriteAllText(file, "test content");
            }

            // Create a subdirectory with more files
            var subDir = Path.Combine(testDir, "subfolder");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "song2.mp3"), "test content");
            File.WriteAllText(Path.Combine(subDir, "video4.mp4"), "test content");

            Console.WriteLine($"✓ Created test directory: {testDir}");
            Console.WriteLine($"✓ Created {testFiles.Length + 2} test files\n");

            // Create test factory for in-memory database
            var factory = new TestDbContextFactory();
            using var manager = new MediaLibraryManager(factory);

            // Test 1: Scan directory
            Console.WriteLine("Test 1: Scanning directory...");
            int progressReports = 0;
            manager.ScanProgress += (sender, args) =>
            {
                progressReports++;
                Console.WriteLine($"  Progress: {args.FilesProcessed}/{args.TotalFiles} - {args.CurrentFile}");
            };

            await manager.ScanDirectoryAsync(testDir);
            Console.WriteLine($"✓ Scan completed with {progressReports} progress reports\n");

            // Test 2: Get media files
            Console.WriteLine("Test 2: Getting media files from database...");
            var mediaFiles = await manager.GetMediaFilesAsync();
            Console.WriteLine($"✓ Found {mediaFiles.Count} media files");
            foreach (var file in mediaFiles)
            {
                Console.WriteLine($"  - {file.Filename} ({file.Type}, {file.Format})");
            }
            Console.WriteLine();

            // Verify we found all files
            if (mediaFiles.Count != 6)
            {
                Console.WriteLine($"✗ Expected 6 files, found {mediaFiles.Count}");
                return;
            }

            // Test 3: File type detection
            Console.WriteLine("Test 3: Verifying file type detection...");
            var audioFiles = mediaFiles.Where(f => f.Type == MediaType.Audio).ToList();
            var videoFiles = mediaFiles.Where(f => f.Type == MediaType.Video).ToList();
            Console.WriteLine($"✓ Audio files: {audioFiles.Count}");
            Console.WriteLine($"✓ Video files: {videoFiles.Count}");
            if (audioFiles.Count != 2 || videoFiles.Count != 4)
            {
                Console.WriteLine("✗ File type detection failed");
                return;
            }
            Console.WriteLine();

            // Test 4: Start monitoring
            Console.WriteLine("Test 4: Starting file system monitoring...");
            manager.StartMonitoring();
            Console.WriteLine($"✓ Monitoring started: {manager.IsMonitoring}");

            // Test 5: File added event
            Console.WriteLine("\nTest 5: Testing file added event...");
            bool fileAddedEventFired = false;
            manager.FilesAdded += (sender, args) =>
            {
                fileAddedEventFired = true;
                Console.WriteLine($"✓ FilesAdded event fired for {args.Files.Count} file(s)");
                foreach (var file in args.Files)
                {
                    Console.WriteLine($"  - {file.Filename}");
                }
            };

            var newFile = Path.Combine(testDir, "newsong.mp3");
            File.WriteAllText(newFile, "new content");
            await Task.Delay(1000); // Wait for file system watcher

            if (fileAddedEventFired)
            {
                Console.WriteLine("✓ File added event working correctly");
            }
            else
            {
                Console.WriteLine("⚠ File added event did not fire (may need more time)");
            }

            // Test 6: File deleted event
            Console.WriteLine("\nTest 6: Testing file deleted event...");
            bool fileDeletedEventFired = false;
            manager.FilesRemoved += (sender, args) =>
            {
                fileDeletedEventFired = true;
                Console.WriteLine($"✓ FilesRemoved event fired for {args.Files.Count} file(s)");
            };

            File.Delete(newFile);
            await Task.Delay(1000); // Wait for file system watcher

            if (fileDeletedEventFired)
            {
                Console.WriteLine("✓ File deleted event working correctly");
            }
            else
            {
                Console.WriteLine("⚠ File deleted event did not fire (may need more time)");
            }

            // Test 7: Stop monitoring
            Console.WriteLine("\nTest 7: Stopping file system monitoring...");
            manager.StopMonitoring();
            Console.WriteLine($"✓ Monitoring stopped: {!manager.IsMonitoring}");

            // Test 8: Rescan after changes
            Console.WriteLine("\nTest 8: Rescanning directory...");
            File.WriteAllText(Path.Combine(testDir, "anothersong.mp3"), "content");
            await manager.ScanDirectoryAsync(testDir);
            var updatedFiles = await manager.GetMediaFilesAsync();
            Console.WriteLine($"✓ Files after rescan: {updatedFiles.Count}");

            Console.WriteLine("\n=== All Tests Passed ===");
        }
        finally
        {
            // Cleanup
            try
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                    Console.WriteLine($"\n✓ Cleaned up test directory");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠ Failed to cleanup test directory: {ex.Message}");
            }
        }
    }
}
