using Avalonia.Controls;
using Avalonia.Input;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

public partial class YouTubeWindow : Window
{
    public YouTubeWindow()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            var box = this.FindControl<TextBox>("QueryTextBox");
            box?.Focus();
            box?.SelectAll();

            // Opened from a search: show the results right away
            if (DataContext is YouTubeViewModel vm && vm.SearchCommand.CanExecute(null))
                vm.SearchCommand.Execute(null);
        };

        // Downloads go on (they belong to the main window); a running search stops
        Closed += (_, _) => (DataContext as YouTubeViewModel)?.CancelSearch();
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is YouTubeViewModel vm && vm.SearchCommand.CanExecute(null))
        {
            vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
