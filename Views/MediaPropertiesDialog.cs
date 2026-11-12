using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

/// <summary>Properties of a media file (catalog/playlist context menu): read-only, text selectable.</summary>
public static class MediaPropertiesDialog
{
    public static async Task ShowAsync(Window owner, MediaFile file)
    {
        // A stat of a file on a network drive can be slow: not on the UI thread
        // (FileInfo reads the disk on first use, so the rows are built there too)
        var folder = Path.GetDirectoryName(file.FilePath);
        var (rows, folderExists) = await Task.Run(() =>
        {
            FileInfo? info = null;
            try { info = new FileInfo(file.FilePath); info.Refresh(); }
            catch { info = null; }
            return (MediaProperties.Build(file, info), folder != null && Directory.Exists(folder));
        });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        for (var i = 0; i < rows.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = new TextBlock
            {
                Text = rows[i].Label,
                Margin = new Thickness(0, 3, 16, 3),
                Foreground = Brushes.Gray
            };
            var value = new SelectableTextBlock
            {
                Text = rows[i].Value,
                Margin = new Thickness(0, 3),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(label, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
        }

        var dialog = new Window
        {
            Title = $"Properties - {file.DisplayName}",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 700,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var openFolder = new Button { Content = "Open Folder", MinWidth = 100 };
        var close = new Button { Content = "Close", MinWidth = 90, IsDefault = true, IsCancel = true };
        close.Click += (_, _) => dialog.Close();
        openFolder.IsEnabled = folderExists;
        openFolder.Click += async (_, _) =>
        {
            try { await dialog.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder!)); }
            catch (Exception ex) { App.Current?.LoggingService?.LogWarning($"Cannot open folder {folder}: {ex.Message}"); }
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        if (file.ThumbnailLoaded && ThumbnailConverter.Instance.Convert(file.ThumbnailPath, typeof(IImage), null,
                System.Globalization.CultureInfo.InvariantCulture) is IImage thumbnail)
        {
            header.Children.Add(new Border
            {
                Width = 96, Height = 72, CornerRadius = new CornerRadius(4), ClipToBounds = true,
                Child = new Image { Source = thumbnail, Stretch = Stretch.UniformToFill }
            });
        }
        header.Children.Add(new SelectableTextBlock
        {
            Text = file.DisplayName,
            FontWeight = FontWeight.Bold,
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 360
        });

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                header,
                new ScrollViewer { MaxHeight = 480, Content = grid },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { openFolder, close }
                }
            }
        };

        await dialog.ShowDialog(owner);
    }
}
