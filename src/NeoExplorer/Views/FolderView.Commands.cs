using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;
using NeoExplorer.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace NeoExplorer.Views;

/// <summary>
/// Commands on the selected items: the right-click menu, keyboard shortcuts, and renaming in place.
/// </summary>
public sealed partial class FolderView
{
    private ItemViewModel? _renaming;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _invalidNameTipTimer;

    private IReadOnlyList<ItemViewModel> Selection => ActiveList.SelectedItems.Cast<ItemViewModel>().ToList();

    private static bool IsKeyDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private void Items_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Keys typed while renaming belong to the name.
        if (e.OriginalSource is TextBox)
        {
            return;
        }

        bool control = IsKeyDown(VirtualKey.Control);
        bool shift = IsKeyDown(VirtualKey.Shift);
        IReadOnlyList<ItemViewModel> selection = Selection;
        e.Handled = true;
        switch (e.Key)
        {
            case VirtualKey.Enter when selection.Count > 0:
                ViewModel.Open(selection);
                break;
            case VirtualKey.F2 when selection.Count == 1:
                StartRename(selection[0]);
                break;
            case VirtualKey.Delete when selection.Count > 0:
                _ = ViewModel.DeleteAsync(selection, permanently: shift);
                break;
            case VirtualKey.X when control && selection.Count > 0:
                _ = ViewModel.CutAsync(selection);
                break;
            case VirtualKey.C when control && selection.Count > 0:
                _ = ViewModel.CopyAsync(selection);
                break;
            case VirtualKey.V when control && ViewModel.CanPasteHere:
                _ = ViewModel.PasteAsync();
                break;
            case VirtualKey.N when control && shift && ViewModel.CanPasteHere:
                _ = CreateNewAsync(isFolder: true);
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    /// <summary>
    /// Right-click, Shift+F10 or the menu key. On an item, shows the item menu; on empty space, the
    /// folder menu (View, Sort by, Refresh, Paste, New) set on <see cref="Root"/> appears instead.
    /// </summary>
    private void Items_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        var list = (ListViewBase)sender;
        SelectorItem? container = null;
        Point? position = null;
        if (args.TryGetPosition(list, out Point point))
        {
            position = point;
            container = FindContainer(args.OriginalSource as DependencyObject, list);
        }
        else if (list.SelectedItems.Count > 0)
        {
            // From the keyboard: the focused item if it's selected, otherwise the first selected one.
            container = FindContainer(FocusManager.GetFocusedElement(XamlRoot) as DependencyObject, list);
            if (container is null || !container.IsSelected)
            {
                container = list.ContainerFromItem(list.SelectedItems[0]) as SelectorItem;
            }
        }

        if (container?.Content is not ItemViewModel item || item.IsRenaming)
        {
            return;
        }

        // Right-clicking an item outside the selection selects just that item, as in File Explorer.
        if (!container.IsSelected)
        {
            list.SelectedItems.Clear();
            list.SelectedItems.Add(item);
        }

        args.Handled = true;
        MenuFlyout menu = CreateItemMenu(Selection);
        if (position is Point at)
        {
            menu.ShowAt(list, new FlyoutShowOptions { Position = at });
        }
        else
        {
            menu.ShowAt(container);
        }
    }

    private static SelectorItem? FindContainer(DependencyObject? element, ListViewBase list)
    {
        for (; element is not null && element != list; element = VisualTreeHelper.GetParent(element))
        {
            if (element is SelectorItem container)
            {
                return container;
            }
        }

        return null;
    }

    private MenuFlyout CreateItemMenu(IReadOnlyList<ItemViewModel> items)
    {
        ItemViewModel first = items[0];
        bool single = items.Count == 1;
        bool singleFile = single && !first.Item.IsFolder;
        var menu = new MenuFlyout();

        MenuFlyoutItem open = MenuItem("Open", "OpenMenuItem", null, null, () => ViewModel.Open(items));
        open.FontWeight = FontWeights.SemiBold;
        menu.Items.Add(open);
        if (singleFile)
        {
            _ = SetDefaultAppIconAsync(open, first.Item.Path);
            menu.Items.Add(CreateOpenWithMenu(first.Item.Path));
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem("Cut", "CutMenuItem", "", "Ctrl+X", () => _ = ViewModel.CutAsync(items)));
        menu.Items.Add(MenuItem("Copy", "CopyMenuItem", "", "Ctrl+C", () => _ = ViewModel.CopyAsync(items)));
        if (single && first.Item.IsFolder && ShellClipboard.HasFiles())
        {
            // Pastes into the folder, as in File Explorer.
            menu.Items.Add(MenuItem("Paste", "PasteIntoMenuItem", "", null, () => _ = ViewModel.PasteAsync(first.Item.Path)));
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        if (single)
        {
            menu.Items.Add(MenuItem("Rename", "RenameMenuItem", "", "F2", () => StartRename(first)));
        }

        menu.Items.Add(MenuItem("Delete", "DeleteMenuItem", "", "Delete", () => _ = ViewModel.DeleteAsync(items, IsKeyDown(VirtualKey.Shift))));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem("Properties", "PropertiesMenuItem", "", "Alt+Enter", () => ShowProperties(first)));
        return menu;
    }

    private static MenuFlyoutItem MenuItem(string text, string automationId, string? glyph, string? shortcut, Action click)
    {
        var item = new MenuFlyoutItem { Text = text };
        if (glyph is not null)
        {
            item.Icon = new FontIcon { Glyph = glyph };
        }

        if (shortcut is not null)
        {
            item.KeyboardAcceleratorTextOverride = shortcut;
        }

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, automationId);
        item.Click += (_, _) => click();
        return item;
    }

    /// <summary>
    /// Shows the icon of the app that opens the file next to "Open".
    /// </summary>
    private static async Task SetDefaultAppIconAsync(MenuFlyoutItem open, string path)
    {
        AppInfo? app = ShellAssociations.GetDefaultApp(Path.GetExtension(path));
        if (await ShellAssociations.LoadIconAsync(app?.IconReference, 16) is ImageSource icon)
        {
            open.Icon = new ImageIcon { Source = icon };
        }
    }

    /// <summary>
    /// "Open with": the recommended apps, which are looked up while the menu opens, then "Choose another app".
    /// </summary>
    private MenuFlyoutSubItem CreateOpenWithMenu(string path)
    {
        var menu = new MenuFlyoutSubItem { Text = "Open with", Icon = new FontIcon { Glyph = "" } };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(menu, "OpenWithMenuItem");
        menu.Items.Add(MenuItem("Choose another app", "ChooseAppMenuItem", null, null,
            () => _ = ShellAssociations.ShowOpenWithDialogAsync(path, changeDefault: false, App.WindowHandle)));
        _ = AddOpenWithAppsAsync(menu, path);
        return menu;
    }

    private static async Task AddOpenWithAppsAsync(MenuFlyoutSubItem menu, string path)
    {
        IReadOnlyList<AppInfo> apps = await ShellAssociations.GetOpenWithAppsAsync(Path.GetExtension(path));
        if (apps.Count == 0)
        {
            return;
        }

        menu.Items.Insert(0, new MenuFlyoutSeparator());
        foreach (AppInfo app in apps.Reverse())
        {
            var item = new MenuFlyoutItem { Text = app.Name };
            item.Click += (_, _) => _ = ShellAssociations.OpenWithAsync(path, app);
            menu.Items.Insert(0, item);
            if (await ShellAssociations.LoadIconAsync(app.IconReference, 16) is ImageSource icon)
            {
                item.Icon = new ImageIcon { Source = icon };
            }
        }
    }

    private void ContextMenu_Opening(object sender, object e)
    {
        foreach (RadioMenuFlyoutItem item in ViewMenu.Items.OfType<RadioMenuFlyoutItem>())
        {
            item.IsChecked = (string)item.Tag == ViewModel.ViewMode.ToString();
        }

        string direction = ViewModel.SortDescending ? "Descending" : "Ascending";
        foreach (RadioMenuFlyoutItem item in SortMenu.Items.OfType<RadioMenuFlyoutItem>())
        {
            item.IsChecked = (string)item.Tag == ViewModel.SortColumn.ToString() || (string)item.Tag == direction;
        }

        // Search results come from many folders, so there is nowhere to paste into.
        Visibility paste = ViewModel.CanPasteHere ? Visibility.Visible : Visibility.Collapsed;
        PasteSeparator.Visibility = paste;
        PasteMenuItem.Visibility = paste;
        PasteMenuItem.IsEnabled = ShellClipboard.HasFiles();
        NewSeparator.Visibility = paste;
        NewMenu.Visibility = paste;
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.PasteAsync();
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        _ = CreateNewAsync(isFolder: true);
    }

    private void NewTextDocument_Click(object sender, RoutedEventArgs e)
    {
        _ = CreateNewAsync(isFolder: false);
    }

    /// <summary>
    /// Creates a new item and starts editing its name, as File Explorer does.
    /// </summary>
    private async Task CreateNewAsync(bool isFolder)
    {
        if (await ViewModel.CreateNewAsync(isFolder) is ItemViewModel item)
        {
            // After the view has selected the new item and moved the focus to it, which would end the editing.
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => StartRename(item));
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && IsKeyDown(VirtualKey.Menu) && e.OriginalSource is not TextBox && Selection is [ItemViewModel first, ..])
        {
            e.Handled = true;
            ShowProperties(first);
        }
    }

    private void ShowProperties(ItemViewModel item)
    {
        PropertiesWindow.Show(item.Item.Path);
    }

    /// <summary>
    /// Starts editing an item's name in place, with the name selected without its extension.
    /// </summary>
    private void StartRename(ItemViewModel item, string? text = null)
    {
        ListViewBase list = ActiveList;
        list.ScrollIntoView(item);
        list.UpdateLayout();
        _renaming = item;
        item.IsRenaming = true;
        if (list.ContainerFromItem(item) is not SelectorItem container)
        {
            return;
        }

        container.UpdateLayout();
        if (FindDescendant<TextBox>(container) is TextBox box)
        {
            box.Text = text ?? item.Name;
            box.Focus(FocusState.Programmatic);
            box.Select(0, FileNameValidator.RenameSelectionLength(box.Text, item.Item.IsFolder));
        }
    }

    private void RenameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var box = (TextBox)sender;
        if (box.DataContext is not ItemViewModel item)
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = CommitRenameAsync(item, box.Text);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            _renaming = null;
            item.IsRenaming = false;
            InvalidNameTip.IsOpen = false;
            ActiveList.Focus(FocusState.Keyboard);
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Clicking elsewhere keeps the new name, as in File Explorer.
        if (((TextBox)sender).DataContext is ItemViewModel item)
        {
            _ = CommitRenameAsync(item, ((TextBox)sender).Text);
        }
    }

    private void RenameBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        // Characters that can't be in a name are refused as they are typed, with an explanation
        // that goes away by itself after a few seconds, like File Explorer's.
        if (FileNameValidator.ContainsInvalidCharacters(args.NewText))
        {
            args.Cancel = true;
            InvalidNameTip.Target = sender;
            InvalidNameTip.IsOpen = true;
            _invalidNameTipTimer ??= CreateInvalidNameTipTimer();
            _invalidNameTipTimer.Start();
        }
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer CreateInvalidNameTipTimer()
    {
        Microsoft.UI.Dispatching.DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => InvalidNameTip.IsOpen = false;
        return timer;
    }

    private async Task CommitRenameAsync(ItemViewModel item, string text)
    {
        // Enter is followed by losing focus; only the first one renames.
        if (_renaming != item)
        {
            return;
        }

        _renaming = null;
        item.IsRenaming = false;
        InvalidNameTip.IsOpen = false;
        string name = FileNameValidator.Normalize(text);

        // An empty name keeps the old one, as in File Explorer.
        if (name.Length == 0 || name == item.Name)
        {
            ActiveList.Focus(FocusState.Programmatic);
            return;
        }

        if (FileNameValidator.Validate(name) is string error)
        {
            await ShowMessageAsync(error, "OK");
            StartRename(item, name);
            return;
        }

        if (!item.Item.IsFolder
            && FileNameValidator.ChangesExtension(item.Name, name)
            && !await ShowMessageAsync("If you change a file name extension, the file might become unusable.\n\nAre you sure you want to change it?", "Yes", "No"))
        {
            StartRename(item, name);
            return;
        }

        ActiveList.Focus(FocusState.Programmatic);
        await ViewModel.RenameAsync(item, name);
    }

    /// <summary>
    /// Shows a message with File Explorer's wording. Returns true if the first button was chosen.
    /// </summary>
    private async Task<bool> ShowMessageAsync(string message, string primary, string? close = null)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Rename",
            Content = message,
            PrimaryButtonText = primary,
            CloseButtonText = close,
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if ((child as T ?? FindDescendant<T>(child)) is T found)
            {
                return found;
            }
        }

        return null;
    }
}
