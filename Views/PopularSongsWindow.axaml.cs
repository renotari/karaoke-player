using Avalonia.Controls;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

public partial class PopularSongsWindow : Window
{
    public PopularSongsWindow()
    {
        InitializeComponent();

        // The comparison with the library starts when the window is shown
        Opened += async (_, _) =>
        {
            if (DataContext is PopularSongsViewModel vm)
                await vm.RefreshAsync();
        };
    }
}
