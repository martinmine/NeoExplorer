using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        _ = new MarqueeSelection(ItemsList, Root, MarqueeCanvas, MarqueeRectangle);
        _ = new MarqueeSelection(IconsGrid, Root, MarqueeCanvas, MarqueeRectangle);
        InvalidNameTip.Subtitle = FileNameValidator.InvalidCharactersMessage;
        // Alt+Enter is taken before the list sees it, which it doesn't pass on with Alt held.
        PreviewKeyDown += OnPreviewKeyDown;
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
            _viewModel.SelectionRestoring += ViewModel_SelectionRestoring;
        }
    }

    /// <summary>
    /// The list or grid for the current view. Both show the same items; only one is visible.
    /// </summary>
    private ListViewBase ActiveList => ViewModel.IsDetailsView ? ItemsList : IconsGrid;

    public void FocusContent() => ActiveList.Focus(FocusState.Programmatic);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FolderViewModel.ViewMode))
        {
            ApplyIconLayout();
            ReloadVisibleIcons();

            // Carry the selection over to the other view.
            Select(ViewModel.SelectedItems.ToList(), reveal: true);
        }
        else if (e.PropertyName == nameof(FolderViewModel.Items) && IsFocusWithin(ActiveList))
        {
            // Replacing the items removes the focused item, and Windows would move the focus to the first
            // control in the window. Keep it in the list instead, after the selection has been restored.
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusSelection);
        }
    }

    private bool IsFocusWithin(UIElement element)
    {
        for (var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; focused is not null; focused = VisualTreeHelper.GetParent(focused))
        {
            if (focused == element)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Focuses the first selected item, or the list itself when nothing is selected.
    /// </summary>
    private void FocusSelection()
    {
        ListViewBase list = ActiveList;
        if (list.SelectedItems.Count > 0 && list.ContainerFromItem(list.SelectedItems[0]) is Control container)
        {
            container.Focus(FocusState.Programmatic);
        }
        else
        {
            list.Focus(FocusState.Programmatic);
        }
    }

    private void ViewModel_SelectionRestoring(object? sender, SelectionRequest request)
    {
        // Runs after the list has picked up the new items.
        DispatcherQueue.TryEnqueue(() => Select(request.Items, request.Reveal));
    }

    private void Select(IReadOnlyList<ItemViewModel> items, bool reveal)
    {
        ListViewBase list = ActiveList;
        list.SelectedItems.Clear();
        foreach (ItemViewModel item in items)
        {
            list.SelectedItems.Add(item);
        }

        if (reveal && items.Count > 0)
        {
            list.ScrollIntoView(items[0]);
        }
    }

    private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ActiveList))
        {
            ViewModel.SelectedItems = ActiveList.SelectedItems.Cast<ItemViewModel>().ToList();
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
        // Ignore double-clicks on empty space between the items, and in a name being edited.
        if ((e.OriginalSource as FrameworkElement)?.DataContext is ItemViewModel { IsRenaming: false } item)
        {
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
