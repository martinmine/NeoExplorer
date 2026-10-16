using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.ViewModels;

namespace NeoExplorer.Views;

public sealed partial class SidebarView : UserControl
{
    private string _location = "";
    private SidebarViewModel _viewModel = null!;

    public SidebarView()
    {
        InitializeComponent();

        // Added with handledEventsToo, since the scroll viewer handles presses on empty space.
        Scroller.AddHandler(PointerPressedEvent, new PointerEventHandler(Scroller_PointerPressed), handledEventsToo: true);
    }

    /// <summary>
    /// Dragging the empty space around the items moves the window, like the title bar.
    /// </summary>
    private void Scroller_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || IsOnItemOrScrollBar(e.OriginalSource))
        {
            return;
        }

        e.Handled = true;
        WindowDrag.Start(this);
    }

    private bool IsOnItemOrScrollBar(object source)
    {
        for (var element = source as DependencyObject; element is not null && element != Scroller; element = VisualTreeHelper.GetParent(element))
        {
            if (element is TreeViewItem or ScrollBar)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Set once by the owning page before the control loads.
    /// </summary>
    public SidebarViewModel ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;

            // Drives load in the background, possibly after the first location was selected.
            _viewModel.ThisPC.Children.CollectionChanged += (_, _) => Select(_location);
        }
    }

    /// <summary>
    /// Highlights the item for the current location, or nothing if it isn't in the sidebar.
    /// </summary>
    public void Select(string location)
    {
        _location = location;
        SidebarItem? item = ViewModel.Find(location);
        bool isQuickAccess = item is not null && ViewModel.QuickAccessItems.Contains(item);
        QuickAccessTree.SelectedItem = isQuickAccess ? item : null;
        ThisPCTree.SelectedItem = isQuickAccess ? null : item;
    }

    private async void ThisPCTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Item is SidebarItem { ShowsSubfolders: true } item)
        {
            await item.LoadChildrenAsync();

            // The current location may be one of the folders that just appeared.
            Select(_location);
        }
    }

    private void Tree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        ViewModel.Open((SidebarItem)args.InvokedItem);

        // The tree selects whatever was clicked. Put the highlight back on the current location,
        // e.g. after the Recycle Bin opened in File Explorer.
        DispatcherQueue.TryEnqueue(() => Select(_location));
    }
}
