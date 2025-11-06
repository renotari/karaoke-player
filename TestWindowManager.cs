using System;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer
{
    /// <summary>
    /// Test runner for WindowManager
    /// </summary>
    class TestWindowManager
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Window Manager Tests...\n");

            try
            {
                var test = new WindowManagerTest();
                await test.RunAllTests();

                Console.WriteLine("\n✓ All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Test failed with error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
