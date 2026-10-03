using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NeoExplorer.ViewModels;
using Windows.System;

namespace NeoExplorer.Views;

public sealed partial class NetworkView : UserControl
{
    public NetworkView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set once by the owning page before the control loads.
    /// </summary>
    public NetworkViewModel ViewModel { get; set; } = null!;

    public void FocusContent() => ItemsGrid.Focus(FocusState.Programmatic);

    private void ItemsGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is NetworkItemViewModel item)
        {
            ViewModel.Open(item);
        }
    }

    private void ItemsGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ItemsGrid.SelectedItem is NetworkItemViewModel item)
        {
            e.Handled = true;
            ViewModel.Open(item);
        }
    }
}
