using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NeoExplorer.Services;
using NeoExplorer.ViewModels;
using Windows.Foundation;
using Windows.Graphics;

namespace NeoExplorer;

public sealed partial class MainWindow : Window
{
    private const double DefaultWidth = 1200;
    private const double DefaultHeight = 800;

    // Small enough for half of a laptop screen, large enough that the navigation bar still fits.
    private const double MinimumWidth = 640;
    private const double MinimumHeight = 400;

    public MainWindow()
    {
        InitializeComponent();

        // The navigation bar replaces the title bar. It and the empty space in the sidebar move the window, as
        // Windows' own caption regions, so dragging, snapping and double-clicking behave like a real title bar.
        // SetTitleBar isn't used, since it only takes one element; the controls are cut out as passthrough regions.
        ExtendsContentIntoTitleBar = true;
        Page.DragAreasChanged += Page_DragAreasChanged;

        // Layout still runs while the window is torn down, when the regions can no longer be set.
        Closed += (_, _) => Page.DragAreasChanged -= Page_DragAreasChanged;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        RestoreSize();
        AppWindow.Closing += (_, _) => SaveSize();

        UpdateTitle();
        Page.ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private OverlappedPresenter Presenter => (OverlappedPresenter)AppWindow.Presenter;

    /// <summary>
    /// Physical pixels per DPI-independent pixel on the window's current monitor.
    /// </summary>
    private double Scale => GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;

    private void RestoreSize()
    {
        Presenter.PreferredMinimumWidth = (int)(MinimumWidth * Scale);
        Presenter.PreferredMinimumHeight = (int)(MinimumHeight * Scale);

        (double width, double height) = Settings.WindowSize ?? (DefaultWidth, DefaultHeight);
        width = Math.Max(width, MinimumWidth);
        height = Math.Max(height, MinimumHeight);
        AppWindow.Resize(new SizeInt32((int)(width * Scale), (int)(height * Scale)));

        if (Settings.IsMaximized)
        {
            Presenter.Maximize();
        }
    }

    private void SaveSize()
    {
        Settings.IsMaximized = Presenter.State == OverlappedPresenterState.Maximized;

        // Keep the last normal size when maximized or minimized, so restoring it later still works.
        if (Presenter.State == OverlappedPresenterState.Restored)
        {
            Settings.WindowSize = (AppWindow.Size.Width / Scale, AppWindow.Size.Height / Scale);
        }
    }

    private RectInt32[] _captionRects = [];
    private RectInt32[] _passthroughRects = [];

    /// <summary>
    /// Updates the regions after layout or sidebar scrolling, only when they actually moved.
    /// </summary>
    private void Page_DragAreasChanged(object? sender, EventArgs e)
    {
        if (Page.XamlRoot is null)
        {
            return;
        }

        // The regions are in physical pixels.
        double scale = Page.XamlRoot.RasterizationScale;
        RectInt32[] captionRects = ToPixels(Page.DragAreas, scale);
        RectInt32[] passthroughRects = ToPixels(Page.DragAreaControls, scale);
        var source = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        if (!captionRects.SequenceEqual(_captionRects))
        {
            _captionRects = captionRects;
            source.SetRegionRects(NonClientRegionKind.Caption, captionRects);
        }

        if (!passthroughRects.SequenceEqual(_passthroughRects))
        {
            _passthroughRects = passthroughRects;
            source.SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects);
        }
    }

    private static RectInt32[] ToPixels(IEnumerable<Rect> rects, double scale) => rects
        .Select(bounds => new RectInt32(
            (int)Math.Round(bounds.X * scale),
            (int)Math.Round(bounds.Y * scale),
            (int)Math.Round(bounds.Width * scale),
            (int)Math.Round(bounds.Height * scale)))
        .ToArray();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.FolderName))
        {
            UpdateTitle();
        }
    }

    // The window title is not shown in the title bar, but appears in the taskbar and Alt+Tab.
    private void UpdateTitle() => Title = Page.ViewModel.FolderName;
}
