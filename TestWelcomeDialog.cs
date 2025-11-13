using System;
using System.Threading.Tasks;
using KaraokePlayer.Views;
using KaraokePlayer.Services;
using KaraokePlayer.Models;
using Microsoft.EntityFrameworkCore;
using LibVLCSharp.Shared;

namespace KaraokePlayer;

public class TestWelcomeDialog
{
    public static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Testing WelcomeDialog instantiation...");

            // Set up database
            var userDataPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KaraokePlayer"
            );
            System.IO.Directory.CreateDirectory(userDataPath);

            // Create test factory for in-memory database
            var factory = new TestDbContextFactory();

            // Initialize LibVLC
            Core.Initialize();
            var libVLC = new LibVLC();

            // Create services
            var mediaLibraryManager = new MediaLibraryManager(factory);
            var metadataExtractor = new MetadataExtractor(factory);
            var thumbnailGenerator = new ThumbnailGenerator(factory, libVLC);

            Console.WriteLine("Services created successfully");

            // Try to create welcome dialog
            var welcomeDialog = new WelcomeDialog(
                mediaLibraryManager,
                metadataExtractor,
                thumbnailGenerator
            );

            Console.WriteLine("WelcomeDialog created successfully!");
            Console.WriteLine($"Selected directory: {welcomeDialog.SelectedMediaDirectory}");

            // Cleanup
            libVLC.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
