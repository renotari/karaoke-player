using System;
using System.Threading.Tasks;

namespace KaraokePlayer;

/// <summary>
/// Simple runner for integration tests.
/// Execute with: dotnet run --project KaraokePlayer.csproj RunIntegrationTests.cs
/// </summary>
public class RunIntegrationTests
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     Karaoke Player - Integration Test Suite               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            var tests = new IntegrationTests();
            await tests.RunAllTestsAsync();
            
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              ALL TESTS PASSED ✓                            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              TESTS FAILED ✗                                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack Trace:");
            Console.WriteLine(ex.StackTrace);
            
            Environment.Exit(1);
        }
    }
}
