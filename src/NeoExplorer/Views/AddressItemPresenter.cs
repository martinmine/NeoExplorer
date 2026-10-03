using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.ViewModels;

namespace NeoExplorer.Views;

/// <summary>
/// Hosts an address bar segment and highlights it on hover and press, like File Explorer.
/// The look is defined by its style's "CommonStates" visual states. The leading computer
/// segment is shown with <see cref="ComputerTemplate"/>, every other one with <see cref="SegmentTemplate"/>.
/// </summary>
public sealed partial class AddressItemPresenter : ContentControl
{
    public DataTemplate? ComputerTemplate { get; set; }

    public DataTemplate? SegmentTemplate { get; set; }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        ContentTemplate = newContent is ComputerSegment ? ComputerTemplate : SegmentTemplate;
    }

    private readonly PointerEventHandler _captureLostHandler;
    private ButtonBase? _button;
    private bool _isPointerOver;
    private bool _isPressed;

    public AddressItemPresenter()
    {
        _captureLostHandler = (_, _) => SetPressed(false);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        UpdateState();
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        UpdateState();
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);

        // The current (last) segment has no button, so it is not clickable and shows no press.
        if (_button is not null)
        {
            SetPressed(true);
        }
    }

    // The breadcrumb item's button captures the pointer on press, so the release only reaches
    // that button. Its capture ending is what tells us the press is over.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(this);
        while (parent is not null and not ButtonBase)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        _button = parent as ButtonBase;
        _button?.AddHandler(PointerCaptureLostEvent, _captureLostHandler, true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _button?.RemoveHandler(PointerCaptureLostEvent, _captureLostHandler);
        _button = null;
        _isPointerOver = false;
        SetPressed(false);
    }

    private void SetPressed(bool isPressed)
    {
        _isPressed = isPressed;
        UpdateState();
    }

    private void UpdateState()
    {
        string state = _isPressed ? "Pressed" : _isPointerOver ? "PointerOver" : "Normal";
        VisualStateManager.GoToState(this, state, false);
    }
}
