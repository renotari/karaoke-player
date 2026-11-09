using System;
using System.Threading;

namespace KaraokePlayer.Services;

/// <summary>
/// Failed checks of the print-based test suites. Every "✗" line goes through
/// <see cref="Fail"/>; the test modes in Program exit with code 1 when any was
/// recorded, so a suite that only prints a failure still fails the run.
/// </summary>
public static class TestFailures
{
    private static int _count;

    public static int Count => _count;

    public static void Fail(string message)
    {
        Interlocked.Increment(ref _count);
        Console.WriteLine(message);
    }
}
