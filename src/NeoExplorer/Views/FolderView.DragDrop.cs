using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;
using NeoExplorer.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Storage;
using DragEventArgs = Microsoft.UI.Xaml.DragEventArgs;

namespace NeoExplorer.Views;

/// <summary>
/// Dragging files and folders out of the view, and dropping them onto folders or empty space,
/// within NeoExplorer and to and from File Explorer and other apps.
/// </summary>
public sealed partial class FolderView
{
    // The items being dragged from this view, known without asking the data package.
    private IReadOnlyList<string>? _draggedPaths;

    // The paths in a drag from another app, read once per drag rather than on every DragOver.
    private DataPackageView? _dragView;
    private IReadOnlyList<string> _dragViewPaths = [];

    private SelectorItem? _dropTarget;

    private void Items_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.OfType<ItemViewModel>().Any(i => i.IsRenaming))
        {
            e.Cancel = true;
            return;
        }

        List<string> paths = e.Items.OfType<ItemViewModel>().Select(i => i.Item.Path).ToList();
        _draggedPaths = paths;
        e.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move;

        // Creating storage items takes a moment, so it's done only when a drop target asks for them.
        e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            DataProviderDeferral deferral = request.GetDeferral();
            try
            {
                request.SetData(await ShellClipboard.GetStorageItemsAsync(paths));
            }
            finally
            {
                deferral.Complete();
            }
        });
    }

    private void Items_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        _draggedPaths = null;
    }

    private async void Items_DragOver(object sender, DragEventArgs e)
    {
        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            (string? folder, DropOperation operation) = await GetDropAsync((ListViewBase)sender, e);
            e.AcceptedOperation = ToDataPackageOperation(operation);
            if (folder is not null && operation != DropOperation.None)
            {
                string name = Path.GetFileName(folder.TrimEnd('\\')) is { Length: > 0 } leaf ? leaf : folder;
                e.DragUIOverride.Caption = $"{(operation == DropOperation.Move ? "Move" : "Copy")} to {name}";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void Items_DragLeave(object sender, DragEventArgs e)
    {
        HighlightDropTarget(null);
    }

    private async void Items_Drop(object sender, DragEventArgs e)
    {
        HighlightDropTarget(null);
        DragOperationDeferral deferral = e.GetDeferral();
        string? folder;
        DropOperation operation;
        IReadOnlyList<string> paths;
        try
        {
            (folder, operation) = await GetDropAsync((ListViewBase)sender, e, highlight: false);
            paths = await GetDraggedPathsAsync(e.DataView);
            e.AcceptedOperation = ToDataPackageOperation(operation);
        }
        finally
        {
            // Let the app the items came from carry on; the copy or move itself can take a while.
            deferral.Complete();
        }

        if (folder is not null && operation != DropOperation.None)
        {
            await ViewModel.DropAsync(paths, folder, operation);
        }
    }

    /// <summary>
    /// Where a drop would go and what it would do. Dropping onto a folder puts the items in it; dropping onto
    /// a file or empty space puts them in the folder shown. Search results have no folder of their own.
    /// </summary>
    private async Task<(string? Folder, DropOperation Operation)> GetDropAsync(ListViewBase list, DragEventArgs e, bool highlight = true)
    {
        // Drag events come from the list itself, so find the item under the pointer.
        SelectorItem? container = VisualTreeHelper.FindElementsInHostCoordinates(e.GetPosition(null), list)
            .OfType<SelectorItem>()
            .FirstOrDefault();
        bool onFolder = container?.Content is ItemViewModel { Item.IsFolder: true };
        string? folder = onFolder ? ((ItemViewModel)container!.Content).Item.Path
            : ViewModel.CanPasteHere ? ViewModel.Location
            : null;
        if (highlight)
        {
            HighlightDropTarget(onFolder ? container : null);
        }

        if (folder is null || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return (null, DropOperation.None);
        }

        IReadOnlyList<string> paths = await GetDraggedPathsAsync(e.DataView);
        DropOperation operation = DropEffects.Choose(
            paths,
            folder,
            control: e.Modifiers.HasFlag(DragDropModifiers.Control),
            shift: e.Modifiers.HasFlag(DragDropModifiers.Shift));

        // The app the items come from may allow only one of the two, e.g. copying from a read-only location.
        if (operation == DropOperation.Move && !e.AllowedOperations.HasFlag(DataPackageOperation.Move))
        {
            operation = e.AllowedOperations.HasFlag(DataPackageOperation.Copy) ? DropOperation.Copy : DropOperation.None;
        }
        else if (operation == DropOperation.Copy && !e.AllowedOperations.HasFlag(DataPackageOperation.Copy))
        {
            operation = DropOperation.None;
        }

        return (folder, operation);
    }

    private async Task<IReadOnlyList<string>> GetDraggedPathsAsync(DataPackageView view)
    {
        if (_draggedPaths is not null)
        {
            return _draggedPaths;
        }

        if (!ReferenceEquals(view, _dragView))
        {
            _dragView = view;
            _dragViewPaths = [];
            try
            {
                IReadOnlyList<IStorageItem> items = await view.GetStorageItemsAsync();
                _dragViewPaths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
            }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
            {
                // Not files on disk, e.g. items from a phone; they can't be dropped here.
            }
        }

        return _dragViewPaths;
    }

    /// <summary>
    /// Highlights the folder the items would be dropped into, like hovering over it.
    /// </summary>
    private void HighlightDropTarget(SelectorItem? container)
    {
        if (container == _dropTarget)
        {
            return;
        }

        // The item doesn't get pointer-over by itself while something is dragged over it.
        if (_dropTarget is not null)
        {
            VisualStateManager.GoToState(_dropTarget, _dropTarget.IsSelected ? "Selected" : "Normal", useTransitions: true);
        }

        _dropTarget = container;
        if (container is not null)
        {
            VisualStateManager.GoToState(container, container.IsSelected ? "PointerOverSelected" : "PointerOver", useTransitions: true);
        }
    }

    private static DataPackageOperation ToDataPackageOperation(DropOperation operation) => operation switch
    {
        DropOperation.Copy => DataPackageOperation.Copy,
        DropOperation.Move => DataPackageOperation.Move,
        _ => DataPackageOperation.None,
    };
}
