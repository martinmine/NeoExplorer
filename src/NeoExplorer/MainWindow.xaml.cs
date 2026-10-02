using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NeoExplorer.ViewModels;
using Windows.Graphics;

namespace NeoExplorer;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // The navigation bar replaces the title bar. Only DragRegion moves the window,
        // so the rest of the bar stays clickable.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(Page.DragRegion);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        SetStartupSize(1200, 800);

        UpdateTitle();
        Page.ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    /// <summary>
    /// Sizes the window in DPI-independent pixels.
    /// </summary>
    private void SetStartupSize(int width, int height)
    {
        double scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
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
