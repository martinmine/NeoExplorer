using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NeoExplorer.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NeoExplorer.ViewModels;

public partial class ItemViewModel(FileSystemItem item) : ObservableObject
{
    private bool _iconRequested;

    public FileSystemItem Item { get; } = item;

    public string Name => Item.Name;

    public string Type => Item.Type;

    public string DateModified => Item.DateModified.ToString("g");

    public string Size => Item.Size is long size ? SizeFormatter.Format(size) : "";

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    /// <summary>
    /// Loads the icon the first time the item scrolls into view. Must be called on the UI thread.
    /// </summary>
    public async Task LoadIconAsync()
    {
        if (_iconRequested)
        {
            return;
        }

        _iconRequested = true;
        try
        {
            IStorageItemProperties storageItem = Item.IsFolder
                ? await StorageFolder.GetFolderFromPathAsync(Item.Path)
                : await StorageFile.GetFileFromPathAsync(Item.Path);
            using StorageItemThumbnail? thumbnail = await storageItem.GetThumbnailAsync(ThumbnailMode.ListView, 16, ThumbnailOptions.UseCurrentScale);
            if (thumbnail is not null)
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
                Icon = bitmap;
            }
        }
        catch (Exception)
        {
            // Icons are best effort; the item still shows without one.
        }
    }

    // Used by UI Automation (screen readers) as the item's name.
    public override string ToString() => Name;
}
