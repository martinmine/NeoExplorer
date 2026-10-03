using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NeoExplorer.Core;
using NeoExplorer.Services;
using NeoExplorer.ViewModels;
using Windows.Graphics;
using Windows.System;

namespace NeoExplorer.Views;

/// <summary>
/// The Properties window for a file or folder, with the General and Details tabs of File Explorer's.
/// </summary>
public sealed partial class PropertiesWindow : Window
{
    private const double WindowWidth = 420;
    private const double WindowHeight = 600;

    // Each new window opens a bit further down and to the right, so they don't cover each other exactly.
    private const int CascadeOffset = 32;

    // One window per item; opening Properties again brings it to the front.
    private static readonly Dictionary<string, PropertiesWindow> OpenWindows = new(StringComparer.OrdinalIgnoreCase);

    private PropertiesWindow(string path)
    {
        ViewModel = new PropertiesViewModel(path);
        InitializeComponent();

        // Like File Explorer's Properties dialog, there is no icon in the title bar. The taskbar still shows the app icon.
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        double scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        PointInt32 owner = App.Window.AppWindow.Position;
        int offset = CascadeOffset * (OpenWindows.Count + 1);
        AppWindow.MoveAndResize(new RectInt32(owner.X + offset, owner.Y + offset, (int)(WindowWidth * scale), (int)(WindowHeight * scale)));

        // File Explorer puts a file's two attributes on one line, and a folder's on two.
        AttributesPanel.Orientation = ViewModel.IsFolder ? Orientation.Vertical : Orientation.Horizontal;
        AttributesPanel.Spacing = ViewModel.IsFolder ? 0 : 16;

        UpdateTitle();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Root.Loaded += Root_Loaded;
        Closed += (_, _) =>
        {
            ViewModel.Cancel();
            OpenWindows.Remove(ViewModel.Path);
        };
    }

    public PropertiesViewModel ViewModel { get; }

    public string TypeLabel => ViewModel.IsFolder ? "Type:" : "Type of file:";

    public string ReadOnlyText => ViewModel.IsFolder ? "Read-only (Only applies to files in folder)" : "Read-only";

    private nint Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    public static void Show(string path)
    {
        if (!OpenWindows.TryGetValue(path, out PropertiesWindow? window))
        {
            window = new PropertiesWindow(path);
            OpenWindows[path] = window;
        }

        window.Activate();
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        // Starts in the name box with the name selected, as in File Explorer.
        NameBox.Focus(FocusState.Programmatic);
        NameBox.SelectAll();
        await ViewModel.LoadAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PropertiesViewModel.Name))
        {
            UpdateTitle();

            // After a rename, Properties for the item's new path should bring this window up.
            foreach (string oldPath in OpenWindows.Where(w => w.Value == this).Select(w => w.Key).ToList())
            {
                OpenWindows.Remove(oldPath);
            }

            OpenWindows[ViewModel.Path] = this;
        }
    }

    private void UpdateTitle() => Title = $"{ViewModel.Name} Properties";

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        bool general = sender.SelectedItem == GeneralTab;
        GeneralPage.Visibility = general ? Visibility.Visible : Visibility.Collapsed;
        DetailsPage.Visibility = general ? Visibility.Collapsed : Visibility.Visible;
    }

    private void NameBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        // Characters that can't be in a name can't be typed, as in the folder view.
        args.Cancel = FileNameValidator.ContainsInvalidCharacters(args.NewText);
    }

    private async void ChangeButton_Click(object sender, RoutedEventArgs e)
    {
        await ShellAssociations.ShowOpenWithDialogAsync(ViewModel.Path, changeDefault: true, Handle);
        await ViewModel.LoadOpensWithAsync();
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasChanges || await ApplyAsync())
        {
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        await ApplyAsync();
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Enter is OK and Esc is Cancel, as in a dialog. Buttons handle Enter themselves.
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == VirtualKey.Enter && e.OriginalSource is not Button)
        {
            e.Handled = true;
            OkButton_Click(OkButton, new RoutedEventArgs());
        }
    }

    /// <summary>
    /// Checks the new name and asks about attribute changes like File Explorer does, then applies the changes.
    /// Returns false if they were cancelled or couldn't all be applied.
    /// </summary>
    private async Task<bool> ApplyAsync()
    {
        string name = FileNameValidator.Normalize(ViewModel.EditedName);
        if (name.Length == 0)
        {
            // An empty name keeps the old one.
            name = ViewModel.Name;
        }

        if (name != ViewModel.Name)
        {
            if (FileNameValidator.Validate(name) is string error)
            {
                await ShowDialogAsync("Rename", error, "OK");
                return false;
            }

            if (ViewModel.IsFile
                && FileNameValidator.ChangesExtension(ViewModel.Name, name)
                && !await ShowDialogAsync("Rename", "If you change a file name extension, the file might become unusable.\n\nAre you sure you want to change it?", "Yes", "No"))
            {
                return false;
            }
        }

        ViewModel.EditedName = name;
        bool includeContents = false;
        if (ViewModel.IsFolder && (ViewModel.ReadOnlyChange is not null || ViewModel.HiddenChange is not null))
        {
            if (await ConfirmAttributeChangesAsync() is not bool choice)
            {
                return false;
            }

            includeContents = choice;
        }

        return await ViewModel.ApplyAsync(includeContents, Handle);
    }

    /// <summary>
    /// File Explorer's "Confirm Attribute Changes": whether a change to a folder also applies to everything in it.
    /// Returns null if cancelled.
    /// </summary>
    private async Task<bool?> ConfirmAttributeChangesAsync()
    {
        var changes = new List<string>();
        if (ViewModel.ReadOnlyChange is bool readOnly)
        {
            changes.Add(readOnly ? "make read-only" : "unset read-only");
        }

        if (ViewModel.HiddenChange is bool hidden)
        {
            changes.Add(hidden ? "hide" : "unhide");
        }

        var options = new RadioButtons
        {
            Items = { "Apply changes to this folder only", "Apply changes to this folder, subfolders and files" },
            SelectedIndex = 1,
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = "You have chosen to make the following attribute changes:", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = string.Join("\n", changes), Margin = new Thickness(16, 0, 0, 0) });
        content.Children.Add(new TextBlock
        {
            Text = "Do you want to apply this change to this folder only, or do you want to apply it to all subfolders and files as well?",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(options);

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "Confirm Attribute Changes",
            Content = content,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? options.SelectedIndex == 1 : null;
    }

    private async Task<bool> ShowDialogAsync(string title, string message, string primary, string? close = null)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = close,
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
