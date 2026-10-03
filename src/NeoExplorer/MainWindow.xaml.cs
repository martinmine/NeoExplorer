using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NeoExplorer.Services;
using NeoExplorer.ViewModels;
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

        // The navigation bar replaces the title bar. Only DragRegion moves the window,
        // so the rest of the bar stays clickable.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(Page.DragRegion);
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
