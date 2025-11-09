using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KaraokePlayer.Views;

public partial class PlaylistItemControl : UserControl
{
    public PlaylistItemControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
