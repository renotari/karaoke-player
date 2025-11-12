using System;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Dedicated runner for unit tests (Task 31)
/// Tests core services: MetadataExtractor, PlaylistManager, SearchEngine, SettingsManager, CacheManager
/// </summary>
public static class RunUnitTests
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          UNIT TESTS FOR CORE SERVICES (Task 31)           ║");
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine();

        int totalTests = 0;
        int passedTests = 0;
        int failedTests = 0;

        try
        {
            // Test 1: MetadataExtractor - Filename parsing patterns
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("TEST 1: MetadataExtractor - Filename Parsing Patterns");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            try
            {
                await MetadataExtractorTest.RunTests();
                passedTests++;
                Console.WriteLine("✓ MetadataExtractor tests PASSED\n");
            }
            catch (Exception ex)
            {
                failedTests++;
                Console.WriteLine($"✗ MetadataExtractor tests FAILED: {ex.Message}\n");
            }
            totalTests++;

            // Test 2: SettingsManager - Validation logic
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("TEST 2: SettingsManager - Validation Logic");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            try
            {
                await SettingsManagerTest.RunTests();
                passedTests++;
                Console.WriteLine("✓ SettingsManager tests PASSED\n");
            }
            catch (Exception ex)
            {
                failedTests++;
                Console.WriteLine($"✗ SettingsManager tests FAILED: {ex.Message}\n");
            }
            totalTests++;

            // Test 3: CacheManager - Eviction policy
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("TEST 3: CacheManager - LRU Eviction Policy");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            try
            {
                await CacheManagerTest.RunTests();
                passedTests++;
                Console.WriteLine("✓ CacheManager tests PASSED\n");
            }
            catch (Exception ex)
            {
                failedTests++;
                Console.WriteLine($"✗ CacheManager tests FAILED: {ex.Message}\n");
            }
            totalTests++;

            // Test 4: SearchEngine - Query performance and accuracy
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("TEST 4: SearchEngine - Query Performance and Accuracy");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            try
            {
                await SearchEngineTest.RunAllTests();
                passedTests++;
                Console.WriteLine("✓ SearchEngine tests PASSED\n");
            }
            catch (Exception ex)
            {
                failedTests++;
                Console.WriteLine($"✗ SearchEngine tests FAILED: {ex.Message}\n");
            }
            totalTests++;

            // Test 5: PlaylistManager - Operations (add, remove, reorder, shuffle)
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("TEST 5: PlaylistManager - Operations");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            try
            {
                await PlaylistManagerTest.RunAllTests();
                passedTests++;
                Console.WriteLine("✓ PlaylistManager tests PASSED\n");
            }
            catch (Exception ex)
            {
                failedTests++;
                Console.WriteLine($"✗ PlaylistManager tests FAILED: {ex.Message}\n");
            }
            totalTests++;

            // Summary
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      TEST SUMMARY                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║  Total Tests:  {totalTests,3}                                         ║");
            Console.WriteLine($"║  Passed:       {passedTests,3}                                         ║");
            Console.WriteLine($"║  Failed:       {failedTests,3}                                         ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            if (failedTests == 0)
            {
                Console.WriteLine();
                Console.WriteLine("✓✓✓ ALL UNIT TESTS PASSED! ✓✓✓");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"✗✗✗ {failedTests} TEST(S) FAILED ✗✗✗");
                Console.WriteLine();
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    CRITICAL ERROR                          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
