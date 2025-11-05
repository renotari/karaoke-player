using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for managing the media library
/// </summary>
public interface IMediaLibraryManager
{
    /// <summary>
    /// Event raised when files are added to the library
    /// </summary>
    event EventHandler<MediaFilesChangedEventArgs>? FilesAdded;

    /// <summary>
    /// Event raised when files are removed from the library
    /// </summary>
    event EventHandler<MediaFilesChangedEventArgs>? FilesRemoved;

    /// <summary>
    /// Event raised when files are modified
    /// </summary>
    event EventHandler<MediaFilesChangedEventArgs>? FilesModified;

    /// <summary>
    /// Event raised to report scan progress
    /// </summary>
    event EventHandler<ScanProgressEventArgs>? ScanProgress;

    /// <summary>
    /// Scans the specified directory recursively for media files
    /// </summary>
    /// <param name="directoryPath">Path to the directory to scan</param>
    /// <returns>Task that completes when scanning is done</returns>
    Task ScanDirectoryAsync(string directoryPath);

    /// <summary>
    /// Gets all media files from the database
    /// </summary>
    /// <returns>List of media files</returns>
    Task<List<MediaFile>> GetMediaFilesAsync();

    /// <summary>
    /// Starts monitoring the media directory for changes
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stops monitoring the media directory for changes
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Gets whether the file system watcher is currently active
    /// </summary>
    bool IsMonitoring { get; }
}

/// <summary>
/// Event args for media file changes
/// </summary>
public class MediaFilesChangedEventArgs : EventArgs
{
    public List<MediaFile> Files { get; set; } = new();
}

/// <summary>
/// Event args for scan progress reporting
/// </summary>
public class ScanProgressEventArgs : EventArgs
{
    public int FilesProcessed { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public double ProgressPercentage => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100 : 0;
}
