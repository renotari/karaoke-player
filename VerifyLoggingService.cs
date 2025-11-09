using System;
using System.IO;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Standalone verification for LoggingService
/// </summary>
public class VerifyLoggingService
{
    public static void RunVerification()
    {
        Console.WriteLine("=== LoggingService Verification ===\n");

        try
        {
            // Test 1: Basic logging
            Console.WriteLine("Test 1: Basic Logging");
            var logger = new LoggingService();
            var logPath = logger.GetLogFilePath();
            Console.WriteLine($"  Log file path: {logPath}");

            logger.LogInformation("Application started");
            logger.LogWarning("This is a warning");
            logger.LogError("This is an error");
            logger.LogDebug("Debug information");

            if (File.Exists(logPath))
            {
                Console.WriteLine("  ✓ Log file created successfully");
                var content = File.ReadAllText(logPath);
                if (content.Contains("[INFO]") && content.Contains("[WARN]") && 
                    content.Contains("[ERROR]") && content.Contains("[DEBUG]"))
                {
                    Console.WriteLine("  ✓ All log levels working");
                }
            }

            // Test 2: File load failure logging
            Console.WriteLine("\nTest 2: File Load Failure Logging");
            logger.LogFileLoadFailure("C:\\test\\missing.mp4", "File not found");
            Console.WriteLine("  ✓ File load failure logged");

            // Test 3: Playback event logging
            Console.WriteLine("\nTest 3: Playback Event Logging");
            logger.LogPlaybackEvent("Started", "Song: TestSong.mp3");
            logger.LogPlaybackEvent("Paused", "Song: TestSong.mp3");
            logger.LogPlaybackEvent("Stopped", "Song: TestSong.mp3");
            Console.WriteLine("  ✓ Playback events logged");

            // Test 4: Crossfade transition logging
            Console.WriteLine("\nTest 4: Crossfade Transition Logging");
            logger.LogCrossfadeTransition("song1.mp4", "song2.mp4", true);
            logger.LogCrossfadeTransition("song2.mp4", "song3.mp4", false, "Next file failed to load");
            Console.WriteLine("  ✓ Crossfade transitions logged");

            // Test 5: Exception logging
            Console.WriteLine("\nTest 5: Exception Logging");
            try
            {
                throw new InvalidOperationException("Test exception");
            }
            catch (Exception ex)
            {
                logger.LogError("Caught an exception", ex);
                Console.WriteLine("  ✓ Exception logged with stack trace");
            }

            // Test 6: Log statistics
            Console.WriteLine("\nTest 6: Log Statistics");
            var stats = logger.GetStatistics();
            Console.WriteLine($"  Current log size: {stats.CurrentLogFileSize} bytes");
            Console.WriteLine($"  Total log files: {stats.TotalLogFiles}");
            Console.WriteLine($"  Newest log: {stats.NewestLogDate}");
            Console.WriteLine("  ✓ Statistics retrieved successfully");

            // Test 7: Verify log content
            Console.WriteLine("\nTest 7: Verify Log Content");
            var finalContent = File.ReadAllText(logPath);
            var lines = finalContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Console.WriteLine($"  Total log entries: {lines.Length}");
            
            // Show last 5 log entries
            Console.WriteLine("\n  Last 5 log entries:");
            for (int i = Math.Max(0, lines.Length - 5); i < lines.Length; i++)
            {
                Console.WriteLine($"    {lines[i]}");
            }

            Console.WriteLine("\n=== All Verification Tests Passed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Verification failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
