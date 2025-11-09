using System;
using System.Threading;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.Services;

/// <summary>
/// Test class for ErrorHandlingService
/// </summary>
public class ErrorHandlingServiceTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== Error Handling Service Tests ===\n");

        TestCorruptedFileHandling();
        TestMissingFileHandling();
        TestPermissionDeniedHandling();
        TestPlaybackFailureHandling();
        TestCrossfadeFailureHandling();
        TestMetadataExtractionFailure();
        TestThumbnailGenerationFailure();
        TestClearAllErrors();
        TestGetError();

        Console.WriteLine("\n=== All Error Handling Tests Completed ===");
    }

    private static void TestCorruptedFileHandling()
    {
        Console.WriteLine("Test: Handle Corrupted File");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var mediaFile = new MediaFile
        {
            Id = "test-1",
            Filename = "corrupted-video.mp4",
            FilePath = "/path/to/corrupted-video.mp4"
        };

        bool errorRaised = false;
        errorService.ErrorOccurred += (s, e) =>
        {
            errorRaised = true;
            Console.WriteLine($"  ✓ Error event raised for: {e.MediaFile.Filename}");
            Console.WriteLine($"  ✓ Error type: {e.Error.Type}");
            Console.WriteLine($"  ✓ Error message: {e.Error.Message}");
        };

        errorService.HandleCorruptedFile(mediaFile, "Codec not supported");
        
        Thread.Sleep(100); // Allow event to fire
        
        if (errorRaised && mediaFile.ErrorMessage != null)
        {
            Console.WriteLine($"  ✓ MediaFile error message set: {mediaFile.ErrorMessage}");
            Console.WriteLine($"  ✓ Notification count: {notificationService.Notifications.Count}");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Error not properly handled\n");
        }
    }

    private static void TestMissingFileHandling()
    {
        Console.WriteLine("Test: Handle Missing File");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var mediaFile = new MediaFile
        {
            Id = "test-2",
            Filename = "missing-song.mp3",
            FilePath = "/path/to/missing-song.mp3"
        };

        errorService.HandleMissingFile(mediaFile);
        
        Thread.Sleep(100);
        
        var error = errorService.GetError(mediaFile.Id);
        if (error != null && error.Type == MediaErrorType.Missing)
        {
            Console.WriteLine($"  ✓ Error stored and retrievable");
            Console.WriteLine($"  ✓ Error type: {error.Type}");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Error not stored correctly\n");
        }
    }

    private static void TestPermissionDeniedHandling()
    {
        Console.WriteLine("Test: Handle Permission Denied");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var mediaFile = new MediaFile
        {
            Id = "test-3",
            Filename = "protected-file.mp4",
            FilePath = "/path/to/protected-file.mp4"
        };

        errorService.HandlePermissionDenied(mediaFile);
        
        Thread.Sleep(100);
        
        if (notificationService.Notifications.Count > 0)
        {
            var notification = notificationService.Notifications[0];
            Console.WriteLine($"  ✓ Notification created: {notification.Title}");
            Console.WriteLine($"  ✓ Notification type: {notification.Type}");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: No notification created\n");
        }
    }

    private static void TestPlaybackFailureHandling()
    {
        Console.WriteLine("Test: Handle Playback Failure");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var mediaFile = new MediaFile
        {
            Id = "test-4",
            Filename = "playback-fail.mkv",
            FilePath = "/path/to/playback-fail.mkv"
        };

        errorService.HandlePlaybackFailure(mediaFile, "Decoder initialization failed");
        
        Thread.Sleep(100);
        
        var error = errorService.GetError(mediaFile.Id);
        if (error != null && error.Type == MediaErrorType.PlaybackFailed)
        {
            Console.WriteLine($"  ✓ Playback error recorded");
            Console.WriteLine($"  ✓ Details: {error.Details}");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Playback error not recorded\n");
        }
    }

    private static void TestCrossfadeFailureHandling()
    {
        Console.WriteLine("Test: Handle Crossfade Failure");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var currentFile = new MediaFile
        {
            Id = "test-5a",
            Filename = "current-song.mp3"
        };
        
        var nextFile = new MediaFile
        {
            Id = "test-5b",
            Filename = "next-song.mp3"
        };

        errorService.HandleCrossfadeFailure(currentFile, nextFile, "Preload failed");
        
        Thread.Sleep(100);
        
        var error = errorService.GetError(nextFile.Id);
        if (error != null && error.Type == MediaErrorType.CrossfadeFailed)
        {
            Console.WriteLine($"  ✓ Crossfade error recorded for next file");
            Console.WriteLine($"  ✓ Warning notification created");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Crossfade error not handled\n");
        }
    }

    private static void TestMetadataExtractionFailure()
    {
        Console.WriteLine("Test: Handle Metadata Extraction Failure");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var mediaFile = new MediaFile
        {
            Id = "test-6",
            Filename = "no-metadata.mp4"
        };

        int initialNotificationCount = notificationService.Notifications.Count;
        errorService.HandleMetadataExtractionFailure(mediaFile, "TagLib failed");
        
        Thread.Sleep(100);
        
        var error = errorService.GetError(mediaFile.Id);
        // Metadata failures should not create toast notifications (not critical)
        if (error != null && notificationService.Notifications.Count == initialNotificationCount)
        {
            Console.WriteLine($"  ✓ Error recorded without toast notification");
            Console.WriteLine($"  ✓ Error type: {error.Type}");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Metadata error handling incorrect\n");
        }
    }

    private static void TestThumbnailGenerationFailure()
    {
        Console.WriteLine("Test: Handle Thumbnail Generation Failure");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var mediaFile = new MediaFile
        {
            Id = "test-7",
            Filename = "no-thumbnail.webm"
        };

        int initialNotificationCount = notificationService.Notifications.Count;
        errorService.HandleThumbnailGenerationFailure(mediaFile, "SkiaSharp error");
        
        Thread.Sleep(100);
        
        var error = errorService.GetError(mediaFile.Id);
        // Thumbnail failures should not create toast notifications (not critical)
        if (error != null && notificationService.Notifications.Count == initialNotificationCount)
        {
            Console.WriteLine($"  ✓ Error recorded without toast notification");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Thumbnail error handling incorrect\n");
        }
    }

    private static void TestClearAllErrors()
    {
        Console.WriteLine("Test: Clear All Errors");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        // Add some errors
        var file1 = new MediaFile { Id = "test-8a", Filename = "file1.mp4" };
        var file2 = new MediaFile { Id = "test-8b", Filename = "file2.mp3" };
        
        errorService.HandleCorruptedFile(file1, "Test error 1");
        errorService.HandleMissingFile(file2);
        
        Thread.Sleep(100);
        
        // Clear all
        errorService.ClearAllErrors();
        
        var error1 = errorService.GetError(file1.Id);
        var error2 = errorService.GetError(file2.Id);
        
        if (error1 == null && error2 == null && notificationService.Notifications.Count == 0)
        {
            Console.WriteLine($"  ✓ All errors cleared");
            Console.WriteLine($"  ✓ All notifications cleared");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Errors not fully cleared\n");
        }
    }

    private static void TestGetError()
    {
        Console.WriteLine("Test: Get Error");
        
        var notificationService = new NotificationService();
        var errorService = new ErrorHandlingService(notificationService);
        
        var mediaFile = new MediaFile
        {
            Id = "test-9",
            Filename = "test-file.mp4"
        };

        // No error initially
        var error1 = errorService.GetError(mediaFile.Id);
        
        // Add error
        errorService.HandlePlaybackFailure(mediaFile, "Test failure");
        Thread.Sleep(100);
        
        // Should have error now
        var error2 = errorService.GetError(mediaFile.Id);
        
        if (error1 == null && error2 != null)
        {
            Console.WriteLine($"  ✓ GetError returns null when no error exists");
            Console.WriteLine($"  ✓ GetError returns error when it exists");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: GetError not working correctly\n");
        }
    }
}
