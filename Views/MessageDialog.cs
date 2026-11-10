using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace KaraokePlayer.Views;

/// <summary>
/// Small modal dialogs (Avalonia has no message box): a yes/no confirmation
/// and an error/information message with an OK button.
/// </summary>
public static class MessageDialog
{
    /// <summary>Asks a question; true when the user confirms.</summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message,
        string confirmText = "Yes", string cancelText = "Cancel")
    {
        var result = false;
        var dialog = Create(title, message, out var buttons);

        var confirm = new Button { Content = confirmText, MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = cancelText, MinWidth = 90, IsCancel = true };
        confirm.Click += (_, _) => { result = true; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);

        await dialog.ShowDialog(owner);
        return result;
    }

    /// <summary>Shows a message (an error, or what was not found) until OK.</summary>
    public static async Task ShowAsync(Window owner, string title, string message)
    {
        var dialog = Create(title, message, out var buttons);
        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, IsCancel = true };
        ok.Click += (_, _) => dialog.Close();
        buttons.Children.Add(ok);

        await dialog.ShowDialog(owner);
    }

    private static Window Create(string title, string message, out StackPanel buttons)
    {
        buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        return new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 600,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 16 },
                    new ScrollViewer
                    {
                        MaxHeight = 400,
                        Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }
                    },
                    buttons
                }
            }
        };
    }
}
