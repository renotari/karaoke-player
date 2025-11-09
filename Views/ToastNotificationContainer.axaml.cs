using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KaraokePlayer.Services;

namespace KaraokePlayer.Views;

public partial class ToastNotificationContainer : UserControl
{
    private INotificationService? _notificationService;

    public ToastNotificationContainer()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is INotificationService notificationService)
        {
            _notificationService = notificationService;
        }
    }

    private void OnDismissRequested(object? sender, string notificationId)
    {
        _notificationService?.Dismiss(notificationId);
    }
}
