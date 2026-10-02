using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NeoExplorer.Core;
using NeoExplorer.ViewModels;
using Windows.System;

namespace NeoExplorer;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        FolderView.ViewModel = ViewModel.Folder;
        ThisPcView.ViewModel = ViewModel.ThisPc;
        SidebarView.ViewModel = ViewModel.Sidebar;

        Loaded += (_, _) => SidebarView.Select(ViewModel.CurrentLocation);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentLocation))
            {
                SidebarView.Select(ViewModel.CurrentLocation);

                // A new location starts without a search, like File Explorer.
                SearchBox.Text = "";
            }
        };

        // Mouse back/forward buttons, even when a child control already handled the click.
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), handledEventsToo: true);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsXButton1Pressed && ViewModel.GoBackCommand.CanExecute(null))
        {
            ViewModel.GoBackCommand.Execute(null);
        }
        else if (properties.IsXButton2Pressed && ViewModel.GoForwardCommand.CanExecute(null))
        {
            ViewModel.GoForwardCommand.Execute(null);
        }
    }

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Backspace goes back, unless a text box already used it for editing.
        if (e.Key == VirtualKey.Back && ViewModel.GoBackCommand.CanExecute(null))
        {
            e.Handled = true;
            ViewModel.GoBackCommand.Execute(null);
        }
    }

    private void Breadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        ViewModel.Navigate(((PathSegment)args.Item).Path);
    }

    private void PathArea_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, PathArea))
        {
            StartEditingPath(ViewModel.CurrentLocation);
        }
    }

    private void EditPath_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        StartEditingPath(ViewModel.CurrentLocation);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Ignore the box being cleared from code when navigating.
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.Folder.Filter(sender.Text);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _ = ViewModel.Folder.SearchAsync(args.QueryText);
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && SearchBox.Text != "")
        {
            e.Handled = true;
            SearchBox.Text = "";
            ViewModel.Folder.Filter("");
        }
    }

    private void FocusSearch_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SearchBox.Focus(FocusState.Keyboard);
    }

    private void StartEditingPath(string text)
    {
        PathArea.Visibility = Visibility.Collapsed;
        PathTextBox.Visibility = Visibility.Visible;
        PathTextBox.Text = text;
        PathTextBox.SelectAll();
        PathTextBox.Focus(FocusState.Programmatic);
    }

    private void StopEditingPath()
    {
        PathTextBox.Visibility = Visibility.Collapsed;
        PathArea.Visibility = Visibility.Visible;
    }

    private async void PathTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            StopEditingPath();
        }
        else if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            string input = PathTextBox.Text;
            if (ViewModel.TryNavigate(input))
            {
                StopEditingPath();
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "NeoExplorer",
                Content = $"NeoExplorer can't find '{input}'. Check the spelling and try again.",
                CloseButtonText = "OK",
            };
            await dialog.ShowAsync();
            StartEditingPath(input);
        }
    }

    private void PathTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        StopEditingPath();
    }
}
