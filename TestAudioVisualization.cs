using System;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Standalone test runner for AudioVisualizationEngine
/// </summary>
public static class TestAudioVisualization
{
    public static async Task Run()
    {
        Console.WriteLine("Running AudioVisualizationEngine Tests\n");
        Console.WriteLine("========================================\n");

        try
        {
            AudioVisualizationEngineTest.RunTests();

            Console.WriteLine("\n========================================");
            Console.WriteLine("All AudioVisualizationEngine tests passed!");
            Console.WriteLine("========================================\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Test failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }

        await Task.CompletedTask;
    }
}
