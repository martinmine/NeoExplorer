using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NeoExplorer.ViewModels;

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

        UpdateTitle();
        Page.ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

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
