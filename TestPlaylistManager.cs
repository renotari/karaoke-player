using System;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer;

/// <summary>
/// Standalone test runner for PlaylistManager
/// </summary>
public static class TestPlaylistManager
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Testing PlaylistManager Service");
        Console.WriteLine("================================\n");

        await PlaylistManagerTest.RunAllTests();

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
