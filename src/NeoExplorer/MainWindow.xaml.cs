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

        // The navigation bar replaces the title bar. All of it moves the window, except the controls in it,
        // which are cut out as passthrough regions so they stay clickable.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(Page.TitleBar);
        Page.TitleBar.SizeChanged += (_, _) => UpdateTitleBarPassthrough();
        foreach (FrameworkElement control in Page.TitleBarControls)
        {
            control.SizeChanged += (_, _) => UpdateTitleBarPassthrough();
        }

        // The regions are in physical pixels, so they move when the window goes to a monitor with another scale.
        Page.Loaded += (_, _) => Page.XamlRoot.Changed += (_, _) => UpdateTitleBarPassthrough();
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

    private void UpdateTitleBarPassthrough()
    {
        if (Page.XamlRoot is null)
        {
            return;
        }

        double scale = Page.XamlRoot.RasterizationScale;
        RectInt32[] rects = Page.TitleBarControls
            .Select(control => control.TransformToVisual(null).TransformBounds(new Rect(0, 0, control.ActualWidth, control.ActualHeight)))
            .Select(bounds => new RectInt32(
                (int)Math.Round(bounds.X * scale),
                (int)Math.Round(bounds.Y * scale),
                (int)Math.Round(bounds.Width * scale),
                (int)Math.Round(bounds.Height * scale)))
            .ToArray();
        InputNonClientPointerSource.GetForWindowId(AppWindow.Id).SetRegionRects(NonClientRegionKind.Passthrough, rects);
    }

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
