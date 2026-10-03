using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace NeoExplorer.Views;

/// <summary>
/// A thin strip on the edge of the sidebar that resizes it when dragged, like in File Explorer.
/// </summary>
public sealed partial class SidebarSplitter : Grid
{
    private double _startX;
    private double _startWidth;

    public SidebarSplitter()
    {
        // A transparent background makes the whole strip receive pointer input.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    /// <summary>
    /// The sidebar's column. Its MinWidth and MaxWidth limit the drag.
    /// </summary>
    public ColumnDefinition? Column { get; set; }

    /// <summary>
    /// Raised when the user lets go, with the new width.
    /// </summary>
    public event EventHandler<double>? Resized;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Column is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Measured against the parent, since this strip moves as the column changes.
        _startX = e.GetCurrentPoint((UIElement)Parent).Position.X;
        _startWidth = Column.ActualWidth;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Column is null || PointerCaptures is not { Count: > 0 })
        {
            return;
        }

        double width = _startWidth + e.GetCurrentPoint((UIElement)Parent).Position.X - _startX;
        Column.Width = new GridLength(Math.Clamp(width, Column.MinWidth, Column.MaxWidth));
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (Column is null || PointerCaptures is not { Count: > 0 })
        {
            return;
        }

        ReleasePointerCapture(e.Pointer);
        Resized?.Invoke(this, Column.Width.Value);
        e.Handled = true;
    }
}
