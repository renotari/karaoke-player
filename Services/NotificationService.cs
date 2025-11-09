using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Service for managing toast notifications
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ObservableCollection<ToastNotification> _notifications;
    private readonly object _lock = new object();

    public ObservableCollection<ToastNotification> Notifications => _notifications;

    public NotificationService()
    {
        _notifications = new ObservableCollection<ToastNotification>();
    }

    public void ShowInfo(string title, string message, int durationMs = 5000)
    {
        Show(new ToastNotification
        {
            Title = title,
            Message = message,
            Type = ToastType.Info,
            DurationMs = durationMs
        });
    }

    public void ShowSuccess(string title, string message, int durationMs = 5000)
    {
        Show(new ToastNotification
        {
            Title = title,
            Message = message,
            Type = ToastType.Success,
            DurationMs = durationMs
        });
    }

    public void ShowWarning(string title, string message, int durationMs = 5000)
    {
        Show(new ToastNotification
        {
            Title = title,
            Message = message,
            Type = ToastType.Warning,
            DurationMs = durationMs
        });
    }

    public void ShowError(string title, string message, int durationMs = 5000)
    {
        Show(new ToastNotification
        {
            Title = title,
            Message = message,
            Type = ToastType.Error,
            DurationMs = durationMs
        });
    }

    public void Show(ToastNotification notification)
    {
        // Ensure we're on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            lock (_lock)
            {
                _notifications.Add(notification);
            }

            // Auto-dismiss after duration
            Task.Delay(notification.DurationMs).ContinueWith(_ =>
            {
                Dismiss(notification.Id);
            });
        });
    }

    public void Dismiss(string notificationId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_lock)
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    notification.IsVisible = false;
                    // Remove after fade animation (500ms)
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            lock (_lock)
                            {
                                _notifications.Remove(notification);
                            }
                        });
                    });
                }
            }
        });
    }

    public void ClearAll()
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_lock)
            {
                _notifications.Clear();
            }
        });
    }
}
