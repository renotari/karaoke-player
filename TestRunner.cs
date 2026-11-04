using System;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Simple test runner for service tests
/// </summary>
public static class TestRunner
{
    public static async Task Main(string[] args)
    {
        // Initialize ReactiveUI the same way App does (touching RxApp runs its static setup)
        _ = ReactiveUI.RxApp.MainThreadScheduler;

        Console.WriteLine("Running Karaoke Player Service Tests\n");
        Console.WriteLine("=====================================\n");

        try
        {
            // Run MetadataExtractor tests
            await MetadataExtractorTest.RunTests();

            Console.WriteLine();

            // Run SettingsManager tests
            await SettingsManagerTest.RunTests();

            Console.WriteLine();

            // Run CacheManager tests
            await CacheManagerTest.RunTests();

            Console.WriteLine();

            // Run SearchEngine tests
            await SearchEngineTest.RunAllTests();

            Console.WriteLine();

            // Run PlaylistManager tests
            await PlaylistManagerTest.RunAllTests();

            Console.WriteLine();

            // Run MediaPlayerController tests
            await MediaPlayerControllerTest.RunTests();

            Console.WriteLine();

            // Run AudioVisualizationEngine tests
            AudioVisualizationEngineTest.RunTests();

            Console.WriteLine();

            // Run LoggingService tests
            LoggingServiceTest.RunTests();

            Console.WriteLine("\n=====================================");
            Console.WriteLine("All tests completed successfully!");
            Console.WriteLine("=====================================\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Test failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
