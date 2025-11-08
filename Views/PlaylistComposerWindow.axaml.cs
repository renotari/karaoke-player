using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Views;

public partial class PlaylistComposerWindow : Window
{
    public PlaylistComposerWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        
        // Set up drag-and-drop support
        SetupDragAndDrop();
        
        // Set up keyboard shortcuts
        SetupKeyboardShortcuts();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupDragAndDrop()
    {
        // Find the catalog and composition ListBoxes
        var catalogListBox = this.FindControl<ListBox>("CatalogListBox");
        var compositionListBox = this.FindControl<ListBox>("CompositionListBox");

        if (catalogListBox != null)
        {
            // Enable drag from catalog
            catalogListBox.AddHandler(PointerPressedEvent, CatalogListBox_PointerPressed, RoutingStrategies.Tunnel);
            catalogListBox.AddHandler(PointerMovedEvent, CatalogListBox_PointerMoved, RoutingStrategies.Tunnel);
            catalogListBox.AddHandler(PointerReleasedEvent, CatalogListBox_PointerReleased, RoutingStrategies.Tunnel);
        }

        if (compositionListBox != null)
        {
            // Enable drop on composition and reordering
            compositionListBox.AddHandler(DragDrop.DragOverEvent, CompositionListBox_DragOver);
            compositionListBox.AddHandler(DragDrop.DropEvent, CompositionListBox_Drop);
            
            // Enable reordering within composition
            compositionListBox.AddHandler(PointerPressedEvent, CompositionListBox_PointerPressed, RoutingStrategies.Tunnel);
            compositionListBox.AddHandler(PointerMovedEvent, CompositionListBox_PointerMoved, RoutingStrategies.Tunnel);
            compositionListBox.AddHandler(PointerReleasedEvent, CompositionListBox_PointerReleased, RoutingStrategies.Tunnel);
        }
    }

    private void SetupKeyboardShortcuts()
    {
        // Keyboard shortcuts will be implemented in task 21
        // For now, just handle Escape to close
        this.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    // Drag-and-drop implementation for catalog to composition
    private Point? _catalogDragStartPoint;
    private bool _catalogIsDragging;

    private void CatalogListBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _catalogDragStartPoint = e.GetPosition(this);
            _catalogIsDragging = false;
        }
    }

    private void CatalogListBox_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_catalogDragStartPoint.HasValue && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var currentPoint = e.GetPosition(this);
            var diff = currentPoint - _catalogDragStartPoint.Value;

            if (!_catalogIsDragging && (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5))
            {
                _catalogIsDragging = true;
                
                if (DataContext is PlaylistComposerViewModel vm && vm.SelectedCatalogItems?.Count > 0)
                {
                    var data = new DataObject();
                    data.Set("CatalogItems", vm.SelectedCatalogItems);
                    DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
                }
            }
        }
    }

    private void CatalogListBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _catalogDragStartPoint = null;
        _catalogIsDragging = false;
    }

    private void CompositionListBox_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("CatalogItems") || e.Data.Contains("CompositionItem"))
        {
            e.DragEffects = DragDropEffects.Copy | DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void CompositionListBox_Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("CatalogItems"))
        {
            // Handle drop from catalog - will be fully implemented in task 21
            if (DataContext is PlaylistComposerViewModel vm)
            {
                vm.AddSelectedCommand.Execute(System.Reactive.Unit.Default).Subscribe();
            }
        }
        else if (e.Data.Contains("CompositionItem"))
        {
            // Handle reordering within composition
            // This would require more complex logic to determine drop position
            // For now, we'll rely on the Move Up/Down buttons
        }
    }

    // Drag-and-drop implementation for composition reordering
    private Point? _compositionDragStartPoint;
    private bool _compositionIsDragging;

    private void CompositionListBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _compositionDragStartPoint = e.GetPosition(this);
            _compositionIsDragging = false;
        }
    }

    private void CompositionListBox_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_compositionDragStartPoint.HasValue && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var currentPoint = e.GetPosition(this);
            var diff = currentPoint - _compositionDragStartPoint.Value;

            if (!_compositionIsDragging && (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5))
            {
                _compositionIsDragging = true;
                
                if (DataContext is PlaylistComposerViewModel vm && vm.SelectedCompositionItem != null)
                {
                    var data = new DataObject();
                    data.Set("CompositionItem", vm.SelectedCompositionItem);
                    DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
                }
            }
        }
    }

    private void CompositionListBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _compositionDragStartPoint = null;
        _compositionIsDragging = false;
    }
}
