using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

public partial class ItemViewModel(FileSystemItem item, bool isSearchResult = false) : ObservableObject
{
    private uint _iconSize;

    public FileSystemItem Item { get; } = item;

    /// <summary>
    /// Search results show the folder each item is in.
    /// </summary>
    public bool IsSearchResult => isSearchResult;

    public string Folder => System.IO.Path.GetDirectoryName(Item.Path) ?? "";

    public string Name => Item.Name;

    public string Type => Item.Type;

    public string DateModified => Item.DateModified.ToString("g");

    public string Size => Item.Size is long size ? SizeFormatter.Format(size) : "";

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    /// <summary>
    /// True while the name is being edited in place.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRenaming))]
    public partial bool IsRenaming { get; set; }

    public bool IsNotRenaming => !IsRenaming;

    /// <summary>
    /// True after Cut until the item is pasted. Cut items are shown faded, as in File Explorer.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconOpacity))]
    public partial bool IsCut { get; set; } = ShellClipboard.IsCut(item.Path);

    public double IconOpacity => IsCut ? 0.5 : 1;

    /// <summary>
    /// Loads the icon when the item scrolls into view, or when the view needs a different size.
    /// Must be called on the UI thread.
    /// </summary>
    public async Task LoadIconAsync(uint size)
    {
        if (_iconSize == size)
        {
            return;
        }

        _iconSize = size;
        ImageSource? icon = await ShellIcons.LoadAsync(Item.Path, Item.IsFolder, size);

        // Skip if the view switched to another size while this one was loading.
        if (icon is not null && _iconSize == size)
        {
            Icon = icon;
        }
    }

    // Used by UI Automation (screen readers) as the item's name.
    public override string ToString() => Name;
}
