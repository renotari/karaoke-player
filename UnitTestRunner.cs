using System;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Standalone unit test runner for Task 31
/// Run with: dotnet run --project KaraokePlayer.csproj --configuration Debug -- --run-unit-tests
/// </summary>
public class UnitTestRunner
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          UNIT TESTS FOR CORE SERVICES (Task 31)           ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Testing: MetadataExtractor, SettingsManager, CacheManager,");
        Console.WriteLine("         SearchEngine, PlaylistManager");
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
                return 0;
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"✗✗✗ {failedTests} TEST(S) FAILED ✗✗✗");
                Console.WriteLine();
                return 1;
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
            return 1;
        }
    }
}
