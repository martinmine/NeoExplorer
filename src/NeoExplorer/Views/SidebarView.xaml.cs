using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.ViewModels;
using Windows.Foundation;

namespace NeoExplorer.Views;

public sealed partial class SidebarView : UserControl
{
    private string _location = "";
    private SidebarViewModel _viewModel = null!;

    public SidebarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The parts of the sidebar that take clicks, the items and the scroll bar, relative to the window and
    /// cut to what is scrolled into view. The empty space around them moves the window.
    /// </summary>
    public IEnumerable<Rect> ControlBounds()
    {
        Rect viewport = Scroller.TransformToVisual(null).TransformBounds(new Rect(0, 0, Scroller.ActualWidth, Scroller.ActualHeight));
        var pending = new Stack<DependencyObject>([Scroller]);
        while (pending.TryPop(out DependencyObject? parent))
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is not FrameworkElement { Visibility: Visibility.Visible } element)
                {
                    continue;
                }

                if (element is not (TreeViewItem or ScrollBar))
                {
                    pending.Push(element);
                    continue;
                }

                Rect bounds = element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                bounds.Intersect(viewport);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    yield return bounds;
                }
            }
        }
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
