using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.Views;

public partial class ToastNotificationControl : UserControl
{
    public static readonly StyledProperty<INotificationService?> NotificationServiceProperty =
        AvaloniaProperty.Register<ToastNotificationControl, INotificationService?>(nameof(NotificationService));

    public INotificationService? NotificationService
    {
        get => GetValue(NotificationServiceProperty);
        set => SetValue(NotificationServiceProperty, value);
    }

    public ToastNotificationControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ToastNotification notification && NotificationService != null)
        {
            NotificationService.Dismiss(notification.Id);
        }
    }
}
