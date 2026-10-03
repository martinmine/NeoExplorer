using Microsoft.UI.Xaml.Controls;
using NeoExplorer.ViewModels;

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
    /// Set once by the owning page before the control loads.
    /// </summary>
    public SidebarViewModel ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;

            // Drives load in the background, possibly after the first location was selected.
            _viewModel.ThisPC.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SidebarItem.Children))
                {
                    Select(_location);
                }
            };
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

    private void Tree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        ViewModel.Open((SidebarItem)args.InvokedItem);

        // The tree selects whatever was clicked. Put the highlight back on the current location,
        // e.g. after the Recycle Bin opened in File Explorer.
        DispatcherQueue.TryEnqueue(() => Select(_location));
    }
}
