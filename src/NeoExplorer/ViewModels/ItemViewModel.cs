using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NeoExplorer.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NeoExplorer.ViewModels;

public partial class ItemViewModel(FileSystemItem item) : ObservableObject
{
    private uint _iconSize;

    public FileSystemItem Item { get; } = item;

    public string Name => Item.Name;

    public string Type => Item.Type;

    public string DateModified => Item.DateModified.ToString("g");

    public string Size => Item.Size is long size ? SizeFormatter.Format(size) : "";

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    /// <summary>
    /// Loads the icon when the item scrolls into view, or when the view needs a different size.
    /// Sizes above 16 get thumbnails, e.g. photo previews. Must be called on the UI thread.
    /// </summary>
    public async Task LoadIconAsync(uint size)
    {
        if (_iconSize == size)
        {
            return;
        }

        _iconSize = size;
        try
        {
            IStorageItemProperties storageItem = Item.IsFolder
                ? await StorageFolder.GetFolderFromPathAsync(Item.Path)
                : await StorageFile.GetFileFromPathAsync(Item.Path);
            var mode = size <= 16 ? ThumbnailMode.ListView : ThumbnailMode.SingleItem;
            using StorageItemThumbnail? thumbnail = await storageItem.GetThumbnailAsync(mode, size, ThumbnailOptions.UseCurrentScale);
            if (thumbnail is null || _iconSize != size)
            {
                return;
            }

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);

            // Skip if the view switched to another size while this one was loading.
            if (_iconSize == size)
            {
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
