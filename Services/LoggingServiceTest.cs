using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace KaraokePlayer.Services;

/// <summary>
/// Test class for LoggingService
/// </summary>
public class LoggingServiceTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== LoggingService Tests ===\n");

        TestBasicLogging();
        TestLogLevels();
        TestFileLoadFailure();
        TestPlaybackEvent();
        TestCrossfadeTransition();
        TestLogRotation();
        TestStatistics();

        Console.WriteLine("\n=== All LoggingService Tests Completed ===");
    }

    private static void TestBasicLogging()
    {
        Console.WriteLine("Test: Basic Logging");
        
        var logger = new LoggingService();
        var logPath = logger.GetLogFilePath();

        logger.LogInformation("Test information message");
        logger.LogWarning("Test warning message");
        logger.LogError("Test error message");

        // Verify log file exists
        if (File.Exists(logPath))
        {
            var content = File.ReadAllText(logPath);
            var hasInfo = content.Contains("[INFO]") && content.Contains("Test information message");
            var hasWarning = content.Contains("[WARN]") && content.Contains("Test warning message");
            var hasError = content.Contains("[ERROR]") && content.Contains("Test error message");

            if (hasInfo && hasWarning && hasError)
            {
                Console.WriteLine("✓ Basic logging works correctly");
            }
            else
            {
                Console.WriteLine("✗ Basic logging failed - missing expected log entries");
            }
        }
        else
        {
            Console.WriteLine("✗ Log file was not created");
        }
    }

    private static void TestLogLevels()
    {
        Console.WriteLine("\nTest: Log Levels");
        
        var logger = new LoggingService();
        
        logger.LogDebug("Debug message");
        logger.LogInformation("Info message");
        logger.LogWarning("Warning message");
        logger.LogError("Error message");
        logger.LogCritical("Critical message");

        var logPath = logger.GetLogFilePath();
        var content = File.ReadAllText(logPath);

        var hasDebug = content.Contains("[DEBUG]");
        var hasInfo = content.Contains("[INFO]");
        var hasWarn = content.Contains("[WARN]");
        var hasError = content.Contains("[ERROR]");
        var hasCritical = content.Contains("[CRITICAL]");

        if (hasDebug && hasInfo && hasWarn && hasError && hasCritical)
        {
            Console.WriteLine("✓ All log levels work correctly");
        }
        else
        {
            Console.WriteLine("✗ Some log levels are missing");
        }
    }

    private static void TestFileLoadFailure()
    {
        Console.WriteLine("\nTest: File Load Failure Logging");
        
        var logger = new LoggingService();
        logger.LogFileLoadFailure("/path/to/missing/file.mp4", "File not found");

        var logPath = logger.GetLogFilePath();
        var content = File.ReadAllText(logPath);

        if (content.Contains("File load failure") && 
            content.Contains("/path/to/missing/file.mp4") &&
            content.Contains("File not found"))
        {
            Console.WriteLine("✓ File load failure logging works correctly");
        }
        else
        {
            Console.WriteLine("✗ File load failure logging failed");
        }
    }

    private static void TestPlaybackEvent()
    {
        Console.WriteLine("\nTest: Playback Event Logging");
        
        var logger = new LoggingService();
        logger.LogPlaybackEvent("Started", "Song: Test Song.mp3");

        var logPath = logger.GetLogFilePath();
        var content = File.ReadAllText(logPath);

        if (content.Contains("Playback event") && 
            content.Contains("Started") &&
            content.Contains("Song: Test Song.mp3"))
        {
            Console.WriteLine("✓ Playback event logging works correctly");
        }
        else
        {
            Console.WriteLine("✗ Playback event logging failed");
        }
    }

    private static void TestCrossfadeTransition()
    {
        Console.WriteLine("\nTest: Crossfade Transition Logging");
        
        var logger = new LoggingService();
        
        // Test successful crossfade
        logger.LogCrossfadeTransition("song1.mp4", "song2.mp4", true);
        
        // Test failed crossfade
        logger.LogCrossfadeTransition("song2.mp4", "song3.mp4", false, "Next file failed to load");

        var logPath = logger.GetLogFilePath();
        var content = File.ReadAllText(logPath);

        var hasSuccess = content.Contains("Crossfade transition") && 
                        content.Contains("song1.mp4 -> song2.mp4") &&
                        content.Contains("SUCCESS");
        
        var hasFailure = content.Contains("song2.mp4 -> song3.mp4") &&
                        content.Contains("FAILED") &&
                        content.Contains("Next file failed to load");

        if (hasSuccess && hasFailure)
        {
            Console.WriteLine("✓ Crossfade transition logging works correctly");
        }
        else
        {
            Console.WriteLine("✗ Crossfade transition logging failed");
        }
    }

    private static void TestLogRotation()
    {
        Console.WriteLine("\nTest: Log Rotation");
        
        var logger = new LoggingService();
        var initialLogPath = logger.GetLogFilePath();

        // Write enough data to trigger rotation (10MB limit)
        var largeMessage = new string('X', 1024 * 100); // 100KB per message
        
        Console.WriteLine("  Writing large log entries to trigger rotation...");
        for (int i = 0; i < 110; i++) // Write ~11MB of data
        {
            logger.LogInformation($"Large message {i}: {largeMessage}");
        }

        // Check if rotation occurred
        var stats = logger.GetStatistics();
        var currentLogPath = logger.GetLogFilePath();

        if (stats.TotalLogFiles > 1)
        {
            Console.WriteLine($"✓ Log rotation works correctly ({stats.TotalLogFiles} log files exist)");
        }
        else
        {
            Console.WriteLine("✗ Log rotation did not occur as expected");
        }
    }

    private static void TestStatistics()
    {
        Console.WriteLine("\nTest: Log Statistics");
        
        var logger = new LoggingService();
        logger.LogInformation("Test message for statistics");

        var stats = logger.GetStatistics();

        if (stats.CurrentLogFileSize > 0 && 
            stats.TotalLogFiles > 0 &&
            stats.NewestLogDate.HasValue)
        {
            Console.WriteLine($"✓ Log statistics work correctly");
            Console.WriteLine($"  Current log size: {stats.CurrentLogFileSize} bytes");
            Console.WriteLine($"  Total log files: {stats.TotalLogFiles}");
            Console.WriteLine($"  Newest log: {stats.NewestLogDate}");
        }
        else
        {
            Console.WriteLine("✗ Log statistics failed");
        }
    }
}
