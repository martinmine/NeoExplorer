using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using NeoExplorer.Core;
using NeoExplorer.ViewModels;
using Windows.System;

namespace NeoExplorer.Views;

public sealed partial class FolderView : UserControl
{
    private const int WheelNotch = 120;

    private FolderViewModel _viewModel = null!;
    private ItemsWrapGrid? _iconsPanel;
    private int _wheelDelta;

    public FolderView()
    {
        InitializeComponent();

        // Ctrl+mouse wheel changes the view, even though the list also handles the wheel for scrolling.
        AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), handledEventsToo: true);
    }

    /// <summary>
    /// Set once by the owning page before the control loads.
    /// </summary>
    public FolderViewModel ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FolderViewModel.ViewMode))
        {
            ApplyIconLayout();
            ReloadVisibleIcons();
        }
    }

    private void IconsPanel_Loaded(object sender, RoutedEventArgs e)
    {
        _iconsPanel = (ItemsWrapGrid)sender;
        ApplyIconLayout();
    }

    /// <summary>
    /// Sizes the tiles so the icon area matches the icon size, with room for two lines of text.
    /// </summary>
    private void ApplyIconLayout()
    {
        if (_iconsPanel is null)
        {
            return;
        }

        double iconSize = ViewModel.IconSize;
        _iconsPanel.ItemWidth = Math.Max(iconSize + 16, 96);
        _iconsPanel.ItemHeight = iconSize + 52;
    }

    /// <summary>
    /// Items already on screen don't get ContainerContentChanging again, so ask them for the new icon size.
    /// </summary>
    private void ReloadVisibleIcons()
    {
        ListViewBase list = ViewModel.IsDetailsView ? ItemsList : IconsGrid;
        list.UpdateLayout();
        foreach (SelectorItem container in list.ItemsPanelRoot?.Children.OfType<SelectorItem>() ?? [])
        {
            if (container.Content is ItemViewModel item)
            {
                _ = item.LoadIconAsync(IconSizeFor(list));
            }
        }
    }

    private uint IconSizeFor(ListViewBase list) => list == ItemsList ? 16 : ViewModel.IconSize;

    private void Items_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is ItemViewModel item)
        {
            _ = item.LoadIconAsync(IconSizeFor(sender));
        }
    }

    private void Items_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Ignore double-clicks on empty space between the items.
        if ((e.OriginalSource as FrameworkElement)?.DataContext is ItemViewModel item)
        {
            ViewModel.Open(item);
        }
    }

    private void Items_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ((ListViewBase)sender).SelectedItem is ItemViewModel item)
        {
            e.Handled = true;
            ViewModel.Open(item);
        }
    }

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SortBy(Enum.Parse<SortColumn>((string)((Button)sender).Tag));
    }

    private void ViewShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.ViewMode = sender.Key switch
        {
            VirtualKey.Number1 => ViewMode.ExtraLargeIcons,
            VirtualKey.Number2 => ViewMode.LargeIcons,
            VirtualKey.Number3 => ViewMode.MediumIcons,
            _ => ViewMode.Details,
        };
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            return;
        }

        // Touchpads send many small deltas, so only step once per full notch.
        e.Handled = true;
        _wheelDelta += e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        int steps = _wheelDelta / WheelNotch;
        if (steps != 0)
        {
            _wheelDelta -= steps * WheelNotch;
            ViewModel.Zoom(steps);
        }
    }

    private void ContextMenu_Opening(object sender, object e)
    {
        foreach (RadioMenuFlyoutItem item in ViewMenu.Items.OfType<RadioMenuFlyoutItem>())
        {
            item.IsChecked = (string)item.Tag == ViewModel.ViewMode.ToString();
        }

        string direction = ViewModel.SortDescending ? "Descending" : "Ascending";
        foreach (RadioMenuFlyoutItem item in SortMenu.Items.OfType<RadioMenuFlyoutItem>())
        {
            item.IsChecked = (string)item.Tag == ViewModel.SortColumn.ToString() || (string)item.Tag == direction;
        }
    }

    private void ViewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ViewMode = Enum.Parse<ViewMode>((string)((FrameworkElement)sender).Tag);
    }

    private void SortMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string tag = (string)((FrameworkElement)sender).Tag;
        if (tag is "Ascending" or "Descending")
        {
            ViewModel.Sort(ViewModel.SortColumn, tag == "Descending");
        }
        else
        {
            ViewModel.Sort(Enum.Parse<SortColumn>(tag), ViewModel.SortDescending);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.RefreshAsync();
    }
}
