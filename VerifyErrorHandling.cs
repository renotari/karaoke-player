using System;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Verification script for error handling and notifications
/// </summary>
public class VerifyErrorHandling
{
    public static async Task RunVerification()
    {
        Console.WriteLine("=== Error Handling Verification ===\n");

        // Create services
        var notificationService = new NotificationService();
        var errorHandlingService = new ErrorHandlingService(notificationService);

        // Subscribe to notifications
        notificationService.Notifications.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ToastNotification notification in e.NewItems)
                {
                    Console.WriteLine($"[{notification.Type}] {notification.Title}: {notification.Message}");
                }
            }
        };

        // Test 1: Corrupted file error
        Console.WriteLine("Test 1: Corrupted File Error");
        var corruptedFile = new MediaFile
        {
            Id = "test1",
            Filename = "corrupted_video.mp4",
            FilePath = "/path/to/corrupted_video.mp4",
            Type = MediaType.Video,
            Format = MediaFormat.MP4
        };
        errorHandlingService.HandleCorruptedFile(corruptedFile, "Invalid codec");
        await Task.Delay(100);
        Console.WriteLine($"Error stored: {errorHandlingService.GetError("test1")?.Message}\n");

        // Test 2: Missing file error
        Console.WriteLine("Test 2: Missing File Error");
        var missingFile = new MediaFile
        {
            Id = "test2",
            Filename = "missing_song.mp3",
            FilePath = "/path/to/missing_song.mp3",
            Type = MediaType.Audio,
            Format = MediaFormat.MP3
        };
        errorHandlingService.HandleMissingFile(missingFile);
        await Task.Delay(100);
        Console.WriteLine($"Error stored: {errorHandlingService.GetError("test2")?.Message}\n");

        // Test 3: Permission denied error
        Console.WriteLine("Test 3: Permission Denied Error");
        var restrictedFile = new MediaFile
        {
            Id = "test3",
            Filename = "restricted.mkv",
            FilePath = "/path/to/restricted.mkv",
            Type = MediaType.Video,
            Format = MediaFormat.MKV
        };
        errorHandlingService.HandlePermissionDenied(restrictedFile);
        await Task.Delay(100);
        Console.WriteLine($"Error stored: {errorHandlingService.GetError("test3")?.Message}\n");

        // Test 4: Crossfade failure
        Console.WriteLine("Test 4: Crossfade Failure");
        var currentFile = new MediaFile
        {
            Id = "current",
            Filename = "current.mp4",
            FilePath = "/path/to/current.mp4",
            Type = MediaType.Video,
            Format = MediaFormat.MP4
        };
        var nextFile = new MediaFile
        {
            Id = "next",
            Filename = "next.mp4",
            FilePath = "/path/to/next.mp4",
            Type = MediaType.Video,
            Format = MediaFormat.MP4
        };
        errorHandlingService.HandleCrossfadeFailure(currentFile, nextFile, "Failed to preload next track");
        await Task.Delay(100);
        Console.WriteLine($"Error stored: {errorHandlingService.GetError("next")?.Message}\n");

        // Test 5: Clear all errors
        Console.WriteLine("Test 5: Clear All Errors");
        errorHandlingService.ClearAllErrors();
        Console.WriteLine($"Errors after clear: {errorHandlingService.GetError("test1") == null}\n");

        // Test 6: Notification types
        Console.WriteLine("Test 6: Different Notification Types");
        notificationService.ShowInfo("Info", "This is an info message");
        await Task.Delay(100);
        notificationService.ShowSuccess("Success", "Operation completed successfully");
        await Task.Delay(100);
        notificationService.ShowWarning("Warning", "This is a warning");
        await Task.Delay(100);
        notificationService.ShowError("Error", "This is an error");
        await Task.Delay(100);

        Console.WriteLine($"\nActive notifications: {notificationService.Notifications.Count}");

        // Test 7: Dismiss notification
        Console.WriteLine("\nTest 7: Dismiss Notification");
        var testNotification = new ToastNotification
        {
            Title = "Test",
            Message = "This will be dismissed",
            Type = ToastType.Info
        };
        notificationService.Show(testNotification);
        await Task.Delay(100);
        Console.WriteLine($"Before dismiss: {notificationService.Notifications.Count} notifications");
        notificationService.Dismiss(testNotification.Id);
        await Task.Delay(600); // Wait for fade animation
        Console.WriteLine($"After dismiss: {notificationService.Notifications.Count} notifications");

        Console.WriteLine("\n=== Verification Complete ===");
    }
}
