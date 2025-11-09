using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KaraokePlayer.Services;

/// <summary>
/// Implementation of logging service with file rotation
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly string _logDirectory;
    private readonly string _logFilePrefix = "karaoke-player";
    private readonly long _maxLogFileSize = 10 * 1024 * 1024; // 10 MB
    private readonly int _maxLogFiles = 10; // Keep last 10 log files
    private readonly object _lockObject = new object();
    private string _currentLogFilePath;

    public LoggingService()
    {
        // Store logs in application data directory
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KaraokePlayer",
            "Logs"
        );

        Directory.CreateDirectory(appDataPath);
        _logDirectory = appDataPath;
        _currentLogFilePath = GetCurrentLogFilePath();

        // Log startup
        LogInformation("Logging service initialized");
    }

    public void LogInformation(string message)
    {
        WriteLog("INFO", message);
    }

    public void LogWarning(string message)
    {
        WriteLog("WARN", message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception != null 
            ? $"{message} | Exception: {exception.GetType().Name} - {exception.Message}\n{exception.StackTrace}"
            : message;
        WriteLog("ERROR", fullMessage);
    }

    public void LogCritical(string message, Exception? exception = null)
    {
        var fullMessage = exception != null 
            ? $"{message} | Exception: {exception.GetType().Name} - {exception.Message}\n{exception.StackTrace}"
            : message;
        WriteLog("CRITICAL", fullMessage);
    }

    public void LogDebug(string message)
    {
        WriteLog("DEBUG", message);
    }

    public void LogFileLoadFailure(string filePath, string reason)
    {
        WriteLog("ERROR", $"File load failure: {filePath} | Reason: {reason}");
    }

    public void LogPlaybackEvent(string eventType, string details)
    {
        WriteLog("INFO", $"Playback event: {eventType} | {details}");
    }

    public void LogCrossfadeTransition(string fromFile, string toFile, bool success, string? errorDetails = null)
    {
        var status = success ? "SUCCESS" : "FAILED";
        var message = $"Crossfade transition: {fromFile} -> {toFile} | Status: {status}";
        
        if (!success && !string.IsNullOrEmpty(errorDetails))
        {
            message += $" | Error: {errorDetails}";
        }

        WriteLog(success ? "INFO" : "ERROR", message);
    }

    public string GetLogFilePath()
    {
        return _currentLogFilePath;
    }

    public LogStatistics GetStatistics()
    {
        lock (_lockObject)
        {
            var logFiles = GetLogFiles();
            var stats = new LogStatistics
            {
                TotalLogFiles = logFiles.Count
            };

            if (File.Exists(_currentLogFilePath))
            {
                var fileInfo = new FileInfo(_currentLogFilePath);
                stats.CurrentLogFileSize = fileInfo.Length;
            }

            if (logFiles.Count > 0)
            {
                stats.OldestLogDate = logFiles.Min(f => f.CreationTime);
                stats.NewestLogDate = logFiles.Max(f => f.LastWriteTime);
            }

            return stats;
        }
    }

    private void WriteLog(string severity, string message)
    {
        lock (_lockObject)
        {
            try
            {
                // Check if rotation is needed
                CheckAndRotateLog();

                // Format: [2024-11-09 14:30:45.123] [INFO] Message
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{severity}] {message}";

                // Append to log file
                File.AppendAllText(_currentLogFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // If logging fails, write to console as fallback
                Console.WriteLine($"Logging failed: {ex.Message}");
                Console.WriteLine($"Original message: [{severity}] {message}");
            }
        }
    }

    private void CheckAndRotateLog()
    {
        if (!File.Exists(_currentLogFilePath))
        {
            return;
        }

        var fileInfo = new FileInfo(_currentLogFilePath);
        
        // Check if current log file exceeds size limit
        if (fileInfo.Length >= _maxLogFileSize)
        {
            RotateLogFile();
        }
    }

    private void RotateLogFile()
    {
        try
        {
            // Generate new log file name with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var rotatedFileName = $"{_logFilePrefix}-{timestamp}.log";
            var rotatedFilePath = Path.Combine(_logDirectory, rotatedFileName);

            // Rename current log file
            if (File.Exists(_currentLogFilePath))
            {
                File.Move(_currentLogFilePath, rotatedFilePath);
            }

            // Update current log file path
            _currentLogFilePath = GetCurrentLogFilePath();

            // Clean up old log files
            CleanupOldLogFiles();

            // Log rotation event
            WriteLog("INFO", $"Log file rotated. New log file: {Path.GetFileName(_currentLogFilePath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Log rotation failed: {ex.Message}");
        }
    }

    private void CleanupOldLogFiles()
    {
        try
        {
            var logFiles = GetLogFiles()
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            // Keep only the most recent log files
            var filesToDelete = logFiles.Skip(_maxLogFiles).ToList();

            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete old log file {file.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup of old log files failed: {ex.Message}");
        }
    }

    private List<FileInfo> GetLogFiles()
    {
        var directory = new DirectoryInfo(_logDirectory);
        
        if (!directory.Exists)
        {
            return new List<FileInfo>();
        }

        return directory.GetFiles($"{_logFilePrefix}*.log")
            .ToList();
    }

    private string GetCurrentLogFilePath()
    {
        return Path.Combine(_logDirectory, $"{_logFilePrefix}.log");
    }
}
