using System;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Test runner for error handling and notification system
/// </summary>
public class TestErrorHandling
{
    public static void RunTests(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Karaoke Player - Error Handling & Notifications Tests   ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // Run notification service tests
            NotificationServiceTest.RunTests();
            
            Console.WriteLine("\n" + new string('─', 60) + "\n");
            
            // Run error handling service tests
            ErrorHandlingServiceTest.RunTests();
            
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    All Tests Completed                     ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Test execution failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
