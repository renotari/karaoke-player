using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
