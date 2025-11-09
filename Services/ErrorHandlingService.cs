using System;
using System.Collections.Concurrent;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for handling errors and coordinating error recovery
/// </summary>
public class ErrorHandlingService : IErrorHandlingService
{
    private readonly INotificationService _notificationService;
    private readonly ILoggingService? _loggingService;
    private readonly ConcurrentDictionary<string, MediaError> _errors;

    public event EventHandler<MediaErrorEventArgs>? ErrorOccurred;

    public ErrorHandlingService(INotificationService notificationService, ILoggingService? loggingService = null)
    {
        _notificationService = notificationService;
        _loggingService = loggingService;
        _errors = new ConcurrentDictionary<string, MediaError>();
    }

    public void HandleCorruptedFile(MediaFile mediaFile, string details)
    {
        var error = new MediaError
        {
            Type = MediaErrorType.Corrupted,
            Message = "File is corrupted or cannot be played",
            Details = details
        };

        _loggingService?.LogFileLoadFailure(mediaFile.FilePath, $"Corrupted: {details}");
        StoreError(mediaFile, error);
        _notificationService.ShowError(
            "Playback Error",
            $"Skipping corrupted file: {mediaFile.Filename}",
            5000
        );
    }

    public void HandleMissingFile(MediaFile mediaFile)
    {
        var error = new MediaError
        {
            Type = MediaErrorType.Missing,
            Message = "File not found",
            Details = $"The file at {mediaFile.FilePath} no longer exists"
        };

        _loggingService?.LogFileLoadFailure(mediaFile.FilePath, "File not found");
        StoreError(mediaFile, error);
        _notificationService.ShowWarning(
            "File Missing",
            $"File not found: {mediaFile.Filename}",
            5000
        );
    }

    public void HandlePermissionDenied(MediaFile mediaFile)
    {
        var error = new MediaError
        {
            Type = MediaErrorType.PermissionDenied,
            Message = "Access denied",
            Details = $"Insufficient permissions to access {mediaFile.FilePath}"
        };

        _loggingService?.LogFileLoadFailure(mediaFile.FilePath, "Permission denied");
        StoreError(mediaFile, error);
        _notificationService.ShowError(
            "Permission Error",
            $"Cannot access file: {mediaFile.Filename}",
            5000
        );
    }

    public void HandlePlaybackFailure(MediaFile mediaFile, string details)
    {
        var error = new MediaError
        {
            Type = MediaErrorType.PlaybackFailed,
            Message = "Playback failed",
            Details = details
        };

        _loggingService?.LogError($"Playback failure for {mediaFile.FilePath}: {details}");
        StoreError(mediaFile, error);
        _notificationService.ShowError(
            "Playback Error",
            $"Failed to play: {mediaFile.Filename}",
            5000
        );
    }

    public void HandleCrossfadeFailure(MediaFile currentFile, MediaFile? nextFile, string details)
    {
        if (nextFile != null)
        {
            var error = new MediaError
            {
                Type = MediaErrorType.CrossfadeFailed,
                Message = "Crossfade transition failed",
                Details = details
            };

            _loggingService?.LogCrossfadeTransition(
                currentFile.Filename, 
                nextFile.Filename, 
                false, 
                details
            );
            StoreError(nextFile, error);
        }

        _notificationService.ShowWarning(
            "Crossfade Failed",
            "Skipping to next valid song",
            3000
        );
    }

    public void HandleMetadataExtractionFailure(MediaFile mediaFile, string details)
    {
        var error = new MediaError
        {
            Type = MediaErrorType.MetadataExtractionFailed,
            Message = "Failed to extract metadata",
            Details = details
        };

        _loggingService?.LogWarning($"Metadata extraction failed for {mediaFile.FilePath}: {details}");
        StoreError(mediaFile, error);
        // Don't show toast for metadata failures - they're not critical
    }

    public void HandleThumbnailGenerationFailure(MediaFile mediaFile, string details)
    {
        var error = new MediaError
        {
            Type = MediaErrorType.ThumbnailGenerationFailed,
            Message = "Failed to generate thumbnail",
            Details = details
        };

        _loggingService?.LogWarning($"Thumbnail generation failed for {mediaFile.FilePath}: {details}");
        StoreError(mediaFile, error);
        // Don't show toast for thumbnail failures - they're not critical
    }

    public void ClearAllErrors()
    {
        _errors.Clear();
        _notificationService.ClearAll();
    }

    public MediaError? GetError(string mediaFileId)
    {
        _errors.TryGetValue(mediaFileId, out var error);
        return error;
    }

    private void StoreError(MediaFile mediaFile, MediaError error)
    {
        _errors[mediaFile.Id] = error;
        mediaFile.ErrorMessage = error.Message;

        ErrorOccurred?.Invoke(this, new MediaErrorEventArgs
        {
            MediaFile = mediaFile,
            Error = error
        });
    }
}
