using System;
using System.Threading;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.Services;

/// <summary>
/// Test class for NotificationService
/// </summary>
public class NotificationServiceTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== Notification Service Tests ===\n");

        TestShowInfo();
        TestShowSuccess();
        TestShowWarning();
        TestShowError();
        TestDismiss();
        TestClearAll();
        TestAutoDismiss();

        Console.WriteLine("\n=== All Notification Tests Completed ===");
    }

    private static void TestShowInfo()
    {
        Console.WriteLine("Test: Show Info Notification");
        
        var service = new NotificationService();
        service.ShowInfo("Test Info", "This is an info message");
        
        Thread.Sleep(100);
        
        if (service.Notifications.Count == 1 && 
            service.Notifications[0].Type == ToastType.Info)
        {
            Console.WriteLine($"  ✓ Info notification created");
            Console.WriteLine($"  ✓ Title: {service.Notifications[0].Title}");
            Console.WriteLine($"  ✓ Message: {service.Notifications[0].Message}");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL\n");
        }
    }

    private static void TestShowSuccess()
    {
        Console.WriteLine("Test: Show Success Notification");
        
        var service = new NotificationService();
        service.ShowSuccess("Test Success", "Operation completed successfully");
        
        Thread.Sleep(100);
        
        if (service.Notifications.Count == 1 && 
            service.Notifications[0].Type == ToastType.Success)
        {
            Console.WriteLine($"  ✓ Success notification created");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL\n");
        }
    }

    private static void TestShowWarning()
    {
        Console.WriteLine("Test: Show Warning Notification");
        
        var service = new NotificationService();
        service.ShowWarning("Test Warning", "This is a warning");
        
        Thread.Sleep(100);
        
        if (service.Notifications.Count == 1 && 
            service.Notifications[0].Type == ToastType.Warning)
        {
            Console.WriteLine($"  ✓ Warning notification created");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL\n");
        }
    }

    private static void TestShowError()
    {
        Console.WriteLine("Test: Show Error Notification");
        
        var service = new NotificationService();
        service.ShowError("Test Error", "An error occurred");
        
        Thread.Sleep(100);
        
        if (service.Notifications.Count == 1 && 
            service.Notifications[0].Type == ToastType.Error)
        {
            Console.WriteLine($"  ✓ Error notification created");
            Console.WriteLine("  ✓ PASS\n");
        }
        else
        {
            Console.WriteLine("  ✗ FAIL\n");
        }
    }

    private static void TestDismiss()
    {
        Console.WriteLine("Test: Dismiss Notification");
        
        var service = new NotificationService();
        service.ShowInfo("Test", "Message");
        
        Thread.Sleep(100);
        
        var notificationId = service.Notifications[0].Id;
        service.Dismiss(notificationId);
        
        Thread.Sleep(100);
        
        // Should be marked as not visible
        if (service.Notifications.Count > 0 && !service.Notifications[0].IsVisible)
        {
            Console.WriteLine($"  ✓ Notification marked as not visible");
            
            // Wait for removal
            Thread.Sleep(600);
            
            if (service.Notifications.Count == 0)
            {
                Console.WriteLine($"  ✓ Notification removed after fade");
                Console.WriteLine("  ✓ PASS\n");
            }
            else
            {
                Console.WriteLine("  ✗ FAIL: Notification not removed\n");
            }
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Notification not dismissed\n");
        }
    }

    private static void TestClearAll()
    {
        Console.WriteLine("Test: Clear All Notifications");
        
        var service = new NotificationService();
        service.ShowInfo("Test 1", "Message 1");
        service.ShowWarning("Test 2", "Message 2");
        service.ShowError("Test 3", "Message 3");
        
        Thread.Sleep(100);
        
        if (service.Notifications.Count == 3)
        {
            Console.WriteLine($"  ✓ Created 3 notifications");
            
            service.ClearAll();
            Thread.Sleep(100);
            
            if (service.Notifications.Count == 0)
            {
                Console.WriteLine($"  ✓ All notifications cleared");
                Console.WriteLine("  ✓ PASS\n");
            }
            else
            {
                Console.WriteLine("  ✗ FAIL: Notifications not cleared\n");
            }
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Notifications not created\n");
        }
    }

    private static void TestAutoDismiss()
    {
        Console.WriteLine("Test: Auto-Dismiss After Duration");
        
        var service = new NotificationService();
        service.ShowInfo("Test", "Message", 1000); // 1 second duration
        
        Thread.Sleep(100);
        
        if (service.Notifications.Count == 1)
        {
            Console.WriteLine($"  ✓ Notification created");
            
            // Wait for auto-dismiss (1000ms + 500ms fade)
            Thread.Sleep(1600);
            
            if (service.Notifications.Count == 0)
            {
                Console.WriteLine($"  ✓ Notification auto-dismissed after duration");
                Console.WriteLine("  ✓ PASS\n");
            }
            else
            {
                Console.WriteLine("  ✗ FAIL: Notification not auto-dismissed\n");
            }
        }
        else
        {
            Console.WriteLine("  ✗ FAIL: Notification not created\n");
        }
    }
}
