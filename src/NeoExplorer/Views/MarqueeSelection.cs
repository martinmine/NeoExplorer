using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using NeoExplorer.Core;
using Windows.Foundation;
using Windows.System;

namespace NeoExplorer.Views;

/// <summary>
/// Selection by dragging a rectangle (marquee) over the items of a list or grid view, starting on empty space,
/// as in File Explorer. Pressing empty space without dragging clears the selection. With Ctrl, the rectangle
/// toggles the items it touches; with Shift, it adds them to the selection.
/// </summary>
internal sealed class MarqueeSelection
{
    // How far the pointer moves before a press becomes a drag, in DPI-independent pixels.
    private const double DragThreshold = 4;

    // Dragging this close to the top or bottom edge scrolls the list.
    private const double ScrollMargin = 24;
    private const double MaxScrollStep = 24;

    private readonly ListViewBase _list;
    private readonly UIElement _host;
    private readonly Canvas _canvas;
    private readonly Rectangle _rectangle;
    private readonly DispatcherQueueTimer _scrollTimer;
    private ScrollViewer? _scrollViewer;
    private uint? _pointerId;
    private bool _dragging;
    private Point _start;
    private Point _pointer;
    private VirtualKeyModifiers _modifiers;
    private HashSet<int> _initialSelection = [];
    private HashSet<int> _selection = [];

    /// <param name="host">An element around the list that receives the pointer while dragging, even outside the list.</param>
    /// <param name="canvas">An overlay on top of the list, where the rectangle is drawn.</param>
    public MarqueeSelection(ListViewBase list, UIElement host, Canvas canvas, Rectangle rectangle)
    {
        _list = list;
        _host = host;
        _canvas = canvas;
        _rectangle = rectangle;
        _scrollTimer = list.DispatcherQueue.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(16);
        _scrollTimer.Tick += (_, _) => AutoScroll();

        // Added with handledEventsToo, since the list's scroll viewer handles presses on empty space. Moves and the
        // release are followed on the host: the list's own parts can take the pointer capture once a drag starts.
        list.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), handledEventsToo: true);
        host.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved), handledEventsToo: true);
        host.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), handledEventsToo: true);
    }

    /// <summary>
    /// Whether a press at <paramref name="source"/> landed on an item (or a scroll bar), rather than on empty space.
    /// </summary>
    public static bool IsOnItem(object source, ListViewBase list)
    {
        for (var element = source as DependencyObject; element is not null && element != list; element = VisualTreeHelper.GetParent(element))
        {
            if (element is SelectorItem or ScrollBar)
            {
                return true;
            }
        }

        return false;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(_list).Properties;
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch
            || !(properties.IsLeftButtonPressed || properties.IsRightButtonPressed)
            || IsOnItem(e.OriginalSource, _list))
        {
            return;
        }

        _modifiers = e.KeyModifiers;
        if (!_modifiers.HasFlag(VirtualKeyModifiers.Control) && !_modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            _list.SelectedItems.Clear();
        }

        _list.Focus(FocusState.Pointer);
        if (!properties.IsLeftButtonPressed || _list.ItemsPanelRoot is not Panel panel)
        {
            return;
        }

        // The start is kept in the coordinates of the items panel, which scrolls with the items,
        // so the rectangle stays anchored to the items when the list scrolls.
        _start = e.GetCurrentPoint(panel).Position;
        _pointer = e.GetCurrentPoint(_list).Position;
        _initialSelection = SelectedIndexes();
        _selection = [.. _initialSelection];
        _dragging = false;

        // Capturing keeps the drag going when the pointer leaves the window.
        _host.CapturePointer(e.Pointer);
        _pointerId = e.Pointer.PointerId;
        _scrollViewer ??= FindScrollViewer(_list);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
            _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        }

        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId != _pointerId)
        {
            return;
        }

        // The release can be missed, e.g. when it happens outside the window after another element took the capture.
        if (!e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed)
        {
            End();
            return;
        }

        _pointer = e.GetCurrentPoint(_list).Position;
        Update();

        bool nearEdge = _dragging && (_pointer.Y < ScrollMargin || _pointer.Y > _list.ActualHeight - ScrollMargin);
        if (nearEdge && !_scrollTimer.IsRunning)
        {
            _scrollTimer.Start();
        }
        else if (!nearEdge)
        {
            _scrollTimer.Stop();
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId == _pointerId)
        {
            _host.ReleasePointerCapture(e.Pointer);
            End();
        }
    }

    private void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_pointerId is not null)
        {
            Update();
        }
    }

    private void End()
    {
        _pointerId = null;
        _dragging = false;
        _scrollTimer.Stop();
        _rectangle.Visibility = Visibility.Collapsed;
    }

    private void AutoScroll()
    {
        if (_scrollViewer is null || _pointerId is null)
        {
            _scrollTimer.Stop();
            return;
        }

        // Scrolls faster the further the pointer is past the edge.
        double distance = _pointer.Y < ScrollMargin ? _pointer.Y - ScrollMargin : _pointer.Y - (_list.ActualHeight - ScrollMargin);
        double step = Math.Clamp(distance / 2, -MaxScrollStep, MaxScrollStep);
        _scrollViewer.ChangeView(null, _scrollViewer.VerticalOffset + step, null, disableAnimation: true);
    }

    private void Update()
    {
        if (_list.ItemsPanelRoot is not Panel panel)
        {
            return;
        }

        Point current = _list.TransformToVisual(panel).TransformPoint(_pointer);
        if (!_dragging)
        {
            if (Math.Abs(current.X - _start.X) < DragThreshold && Math.Abs(current.Y - _start.Y) < DragThreshold)
            {
                return;
            }

            _dragging = true;
            _rectangle.Visibility = Visibility.Visible;
        }

        Bounds area = Bounds.FromCorners(_start.X, _start.Y, current.X, current.Y);
        DrawRectangle(panel, area);

        HashSet<int> touched = [.. GetLayout(panel)?.HitTest(area) ?? []];
        HashSet<int> selection = [.. _initialSelection];
        if (_modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            selection.SymmetricExceptWith(touched);
        }
        else if (_modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            selection.UnionWith(touched);
        }
        else
        {
            selection = touched;
        }

        Select(selection);
    }

    private void DrawRectangle(Panel panel, Bounds area)
    {
        Point topLeft = panel.TransformToVisual(_canvas).TransformPoint(new Point(area.Left, area.Top));
        Canvas.SetLeft(_rectangle, topLeft.X);
        Canvas.SetTop(_rectangle, topLeft.Y);
        _rectangle.Width = area.Right - area.Left;
        _rectangle.Height = area.Bottom - area.Top;
        _canvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, _canvas.ActualWidth, _canvas.ActualHeight) };
    }

    /// <summary>
    /// Changes only the items whose selection differs, so a large selection updates quickly.
    /// </summary>
    private void Select(HashSet<int> selection)
    {
        foreach (int index in _selection.Except(selection))
        {
            _list.DeselectRange(new ItemIndexRange(index, 1));
        }

        foreach (int index in selection.Except(_selection))
        {
            _list.SelectRange(new ItemIndexRange(index, 1));
        }

        _selection = selection;
    }

    private HashSet<int> SelectedIndexes()
    {
        var indexes = new HashSet<int>();
        foreach (ItemIndexRange range in _list.SelectedRanges)
        {
            for (int i = range.FirstIndex; i <= range.LastIndex; i++)
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    /// <summary>
    /// Where the items are, measured from the containers on screen. Items scrolled out of view
    /// have no container, but the list and grid panels space all items evenly.
    /// </summary>
    private ItemGrid? GetLayout(Panel panel)
    {
        var samples = new List<ItemGrid.Sample>();
        double width = 0;
        double height = 0;
        foreach (SelectorItem container in panel.Children.OfType<SelectorItem>())
        {
            int index = _list.IndexFromContainer(container);
            if (index < 0 || container.Visibility != Visibility.Visible)
            {
                // A recycled container that holds no item.
                continue;
            }

            Point position = container.TransformToVisual(panel).TransformPoint(default);
            samples.Add(new ItemGrid.Sample(index, position.X, position.Y));
            width = container.ActualWidth;
            height = container.ActualHeight;
        }

        samples.Sort((a, b) => a.Index.CompareTo(b.Index));
        return panel is ItemsWrapGrid grid
            ? ItemGrid.FromSamples(samples, width, height, grid.ItemWidth, grid.ItemHeight, _list.Items.Count)
            : ItemGrid.FromSamples(samples, width, height, null, null, _list.Items.Count);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if ((child as ScrollViewer ?? FindScrollViewer(child)) is ScrollViewer found)
            {
                return found;
            }
        }

        return null;
    }
}
