using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NeoExplorer.ViewModels;
using Windows.System;

namespace NeoExplorer.Views;

public sealed partial class ThisPcView : UserControl
{
    public ThisPcView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set once by the owning page before the control loads.
    /// </summary>
    public ThisPcViewModel ViewModel { get; set; } = null!;

    private void DrivesGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is DriveViewModel drive)
        {
            ViewModel.Open(drive);
        }
    }

    private void DrivesGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && DrivesGrid.SelectedItem is DriveViewModel drive)
        {
            e.Handled = true;
            ViewModel.Open(drive);
        }
    }
}
