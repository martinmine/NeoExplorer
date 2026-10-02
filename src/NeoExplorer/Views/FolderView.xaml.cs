using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NeoExplorer.Core;
using NeoExplorer.ViewModels;
using Windows.System;

namespace NeoExplorer.Views;

public sealed partial class FolderView : UserControl
{
    public FolderView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set once by the owning page before the control loads.
    /// </summary>
    public FolderViewModel ViewModel { get; set; } = null!;

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SortBy(Enum.Parse<SortColumn>((string)((Button)sender).Tag));
    }

    private void ItemsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is ItemViewModel item)
        {
            _ = item.LoadIconAsync();
        }
    }

    private void ItemsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Ignore double-clicks on empty space below the items.
        if ((e.OriginalSource as FrameworkElement)?.DataContext is ItemViewModel item)
        {
            ViewModel.Open(item);
        }
    }

    private void ItemsList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ItemsList.SelectedItem is ItemViewModel item)
        {
            e.Handled = true;
            ViewModel.Open(item);
        }
    }
}
