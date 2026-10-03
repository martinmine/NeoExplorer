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

    public void FocusContent() => FoldersGrid.Focus(FocusState.Programmatic);

    private void Grid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        Open((e.OriginalSource as FrameworkElement)?.DataContext);

    private void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ((GridView)sender).SelectedItem is { } item)
        {
            e.Handled = true;
            Open(item);
        }
    }

    private void Open(object? item)
    {
        if (item is KnownFolderViewModel folder)
        {
            ViewModel.Open(folder);
        }
        else if (item is DriveViewModel drive)
        {
            ViewModel.Open(drive);
        }
    }
}
