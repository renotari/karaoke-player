using System;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for logging application events, errors, and diagnostics
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Log an informational message
    /// </summary>
    void LogInformation(string message);

    /// <summary>
    /// Log a warning message
    /// </summary>
    void LogWarning(string message);

    /// <summary>
    /// Log an error message
    /// </summary>
    void LogError(string message, Exception? exception = null);

    /// <summary>
    /// Log a critical error message
    /// </summary>
    void LogCritical(string message, Exception? exception = null);

    /// <summary>
    /// Log a debug message
    /// </summary>
    void LogDebug(string message);

    /// <summary>
    /// Log a file load failure
    /// </summary>
    void LogFileLoadFailure(string filePath, string reason);

    /// <summary>
    /// Log a playback event
    /// </summary>
    void LogPlaybackEvent(string eventType, string details);

    /// <summary>
    /// Log a crossfade transition
    /// </summary>
    void LogCrossfadeTransition(string fromFile, string toFile, bool success, string? errorDetails = null);

    /// <summary>
    /// Get the current log file path
    /// </summary>
    string GetLogFilePath();

    /// <summary>
    /// Get log statistics
    /// </summary>
    LogStatistics GetStatistics();
}

/// <summary>
/// Statistics about the logging system
/// </summary>
public class LogStatistics
{
    public long CurrentLogFileSize { get; set; }
    public int TotalLogFiles { get; set; }
    public DateTime? OldestLogDate { get; set; }
    public DateTime? NewestLogDate { get; set; }
}
