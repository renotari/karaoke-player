using Avalonia;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace KaraokePlayer;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check if running in test mode
        if (args.Contains("--test"))
        {
            RunWithTemporaryDataDirectory(RunTests);
            return;
        }

        // Check if running unit tests specifically (Task 31)
        if (args.Contains("--unit-tests"))
        {
            RunWithTemporaryDataDirectory(RunUnitTests);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Tests must never touch the real user data (settings, database, logs):
    /// point AppPaths to a temporary directory and delete it afterwards.
    /// </summary>
    private static void RunWithTemporaryDataDirectory(Func<Task> run)
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KaraokePlayerTests-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(Services.AppPaths.DataDirEnvironmentVariable, tempDir);
        try
        {
            run().GetAwaiter().GetResult();
        }
        finally
        {
            try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }

        // Suites that only print a failed check still fail the run
        if (Services.TestFailures.Count > 0)
        {
            Console.WriteLine($"✗✗✗ {Services.TestFailures.Count} failed check(s) - see the ✗ lines above");
            Environment.Exit(1);
        }
    }

    private static async Task RunTests()
    {
        await TestRunner.Main(Array.Empty<string>());
    }

    private static async Task RunUnitTests()
    {
        await KaraokePlayer.RunUnitTests.Main(Array.Empty<string>());
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
