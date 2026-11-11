using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using KaraokePlayer.ViewModels;

// Avalonia's legacy DataObject drag-and-drop API (still supported in 11.x,
// replaced by DataTransfer/DoDragDropAsync): migrate when moving to Avalonia 12.
#pragma warning disable CS0618

namespace KaraokePlayer.Views;

/// <summary>
/// Reordering the current playlist with drag and drop. The song playing stays
/// on top: it cannot be dragged and nothing can be dropped above it. A line
/// above (or below the last) item shows where the song will land.
/// </summary>
public partial class MainWindow
{
    private const string PlaylistItemFormat = "KaraokePlayer.PlaylistItem";
    private const double DragThreshold = 6;
    private const double AutoScrollMargin = 30;

    private ListBox? _playlistListBox;
    private Point? _playlistDragStart;
    private PlaylistItemViewModel? _playlistDragItem;
    private ListBoxItem? _dropMarker;

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void SetupPlaylistDragAndDrop()
    {
        _playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
        if (_playlistListBox == null)
            return;

        DragDrop.SetAllowDrop(_playlistListBox, true);
        // Tunnel + handled too: the list items handle the press (selection)
        _playlistListBox.AddHandler(PointerPressedEvent, OnPlaylistPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _playlistListBox.AddHandler(PointerMovedEvent, OnPlaylistPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        _playlistListBox.AddHandler(PointerReleasedEvent, (_, _) => CancelPlaylistDrag(), RoutingStrategies.Tunnel, handledEventsToo: true);
        // Enter too: moving onto another item raises DragEnter, not DragOver
        _playlistListBox.AddHandler(DragDrop.DragEnterEvent, OnPlaylistDragOver);
        _playlistListBox.AddHandler(DragDrop.DragOverEvent, OnPlaylistDragOver);
        _playlistListBox.AddHandler(DragDrop.DragLeaveEvent, OnPlaylistDragLeave);
        _playlistListBox.AddHandler(DragDrop.DropEvent, OnPlaylistDrop);
    }

    private void CancelPlaylistDrag()
    {
        _playlistDragStart = null;
        _playlistDragItem = null;
    }

    private void OnPlaylistPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CancelPlaylistDrag();
        if (_playlistListBox == null || !e.GetCurrentPoint(_playlistListBox).Properties.IsLeftButtonPressed)
            return;

        // Not from a button inside the item (remove)
        var source = e.Source as Visual;
        if (source?.FindAncestorOfType<Button>(includeSelf: true) != null)
            return;

        if (source?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is PlaylistItemViewModel item
            && ViewModel?.CanMovePlaylistItem(item) == true)
        {
            _playlistDragStart = e.GetPosition(_playlistListBox);
            _playlistDragItem = item;
        }
    }

    private async void OnPlaylistPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_playlistListBox == null || _playlistDragStart is not { } start || _playlistDragItem is not { } item)
            return;

        if (!e.GetCurrentPoint(_playlistListBox).Properties.IsLeftButtonPressed)
        {
            CancelPlaylistDrag();
            return;
        }

        var delta = e.GetPosition(_playlistListBox) - start;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        CancelPlaylistDrag();
        try
        {
            var data = new DataObject();
            data.Set(PlaylistItemFormat, item);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            App.Current?.LoggingService?.LogError("Playlist drag and drop failed", ex);
        }
        finally
        {
            ShowDropMarker(null, false);
        }
    }

    private void OnPlaylistDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(PlaylistItemFormat) || FindDropIndex(e) is not { } insertBefore)
        {
            e.DragEffects = DragDropEffects.None;
            ShowDropMarker(null, false);
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        AutoScroll(e);

        // The line goes above the item the song will precede, or below the last one
        var count = _playlistListBox!.ItemCount;
        if (insertBefore < count)
            ShowDropMarker(_playlistListBox.ContainerFromIndex(insertBefore) as ListBoxItem, below: false);
        else
            ShowDropMarker(_playlistListBox.ContainerFromIndex(count - 1) as ListBoxItem, below: true);
    }

    private void OnPlaylistDragLeave(object? sender, DragEventArgs e)
    {
        // Raised (and bubbled up) also when moving from one item to the next:
        // hide the line only when the pointer really left the list
        if (_playlistListBox == null)
            return;
        var p = e.GetPosition(_playlistListBox);
        if (!new Rect(_playlistListBox.Bounds.Size).Contains(p))
            ShowDropMarker(null, false);
    }

    private async void OnPlaylistDrop(object? sender, DragEventArgs e)
    {
        ShowDropMarker(null, false);
        if (e.Data.Get(PlaylistItemFormat) is not PlaylistItemViewModel item
            || FindDropIndex(e) is not { } insertBefore
            || ViewModel is not { } vm)
        {
            return;
        }

        try
        {
            await vm.MovePlaylistItemAsync(item, insertBefore);
        }
        catch (Exception ex)
        {
            App.Current?.LoggingService?.LogError("Could not move the song in the playlist", ex);
        }
    }

    /// <summary>
    /// Where the dragged song would land: the index it would be inserted before
    /// (upper half of an item = before it, lower half = after it; below the
    /// last item = at the end). Never above the song playing.
    /// </summary>
    private int? FindDropIndex(DragEventArgs e)
    {
        if (_playlistListBox == null || ViewModel is not { } vm)
            return null;

        var count = _playlistListBox.ItemCount;
        var first = vm.IsSongLoaded ? 1 : 0;
        if (count <= first)
            return null;

        var hit = _playlistListBox.InputHitTest(e.GetPosition(_playlistListBox)) as Visual;
        var container = hit?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        int insertBefore;
        if (container == null)
        {
            insertBefore = count; // empty space under the last song
        }
        else
        {
            var index = _playlistListBox.IndexFromContainer(container);
            if (index < 0)
                return null;
            insertBefore = e.GetPosition(container).Y > container.Bounds.Height / 2 ? index + 1 : index;
        }
        return Math.Max(insertBefore, first);
    }

    /// <summary>Scrolls the list while dragging near its top or bottom edge.</summary>
    private void AutoScroll(DragEventArgs e)
    {
        if (_playlistListBox?.Scroll is not ScrollViewer scroll)
            return;

        var y = e.GetPosition(_playlistListBox).Y;
        var step = 0.0;
        if (y < AutoScrollMargin)
            step = -20;
        else if (y > _playlistListBox.Bounds.Height - AutoScrollMargin)
            step = 20;

        if (step != 0)
        {
            var maxY = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            scroll.Offset = scroll.Offset.WithY(Math.Clamp(scroll.Offset.Y + step, 0, maxY));
        }
    }

    private void ShowDropMarker(ListBoxItem? item, bool below)
    {
        if (_dropMarker != null && (_dropMarker != item || _dropMarker.Classes.Contains("dropBelow") != below))
        {
            _dropMarker.Classes.Remove("dropAbove");
            _dropMarker.Classes.Remove("dropBelow");
        }

        _dropMarker = item;
        item?.Classes.Set(below ? "dropBelow" : "dropAbove", true);
    }
}
