using System;

namespace KaraokePlayer.Models;

/// <summary>
/// Represents a toast notification to be displayed to the user
/// </summary>
public class ToastNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ToastType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int DurationMs { get; set; } = 5000; // Default 5 seconds
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Types of toast notifications
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}
