using System;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Standalone test runner for SearchEngine
/// </summary>
public class TestSearchEngine
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Running SearchEngine Tests...\n");
        
        try
        {
            await SearchEngineTest.RunAllTests();
            Console.WriteLine("\n✓ All tests passed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Tests failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
