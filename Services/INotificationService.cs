using System;
using System.Collections.ObjectModel;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for managing toast notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Observable collection of active notifications
    /// </summary>
    ObservableCollection<ToastNotification> Notifications { get; }

    /// <summary>
    /// Show an info notification
    /// </summary>
    void ShowInfo(string title, string message, int durationMs = 5000);

    /// <summary>
    /// Show a success notification
    /// </summary>
    void ShowSuccess(string title, string message, int durationMs = 5000);

    /// <summary>
    /// Show a warning notification
    /// </summary>
    void ShowWarning(string title, string message, int durationMs = 5000);

    /// <summary>
    /// Show an error notification
    /// </summary>
    void ShowError(string title, string message, int durationMs = 5000);

    /// <summary>
    /// Show a custom notification
    /// </summary>
    void Show(ToastNotification notification);

    /// <summary>
    /// Dismiss a notification by ID
    /// </summary>
    void Dismiss(string notificationId);

    /// <summary>
    /// Clear all notifications
    /// </summary>
    void ClearAll();
}
