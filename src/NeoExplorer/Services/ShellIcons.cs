using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NeoExplorer.Services;

public static class ShellIcons
{
    /// <summary>
    /// Loads the icon of a file, folder or drive. Sizes above 16 get thumbnails, e.g. photo previews.
    /// Returns null if Windows can't provide one. Must be called on the UI thread.
    /// </summary>
    public static async Task<ImageSource?> LoadAsync(string path, bool isFolder, uint size)
    {
        try
        {
            IStorageItemProperties item = isFolder
                ? await StorageFolder.GetFolderFromPathAsync(path)
                : await StorageFile.GetFileFromPathAsync(path);
            var mode = size <= 16 ? ThumbnailMode.ListView : ThumbnailMode.SingleItem;
            using StorageItemThumbnail? thumbnail = await item.GetThumbnailAsync(mode, size, ThumbnailOptions.UseCurrentScale);
            if (thumbnail is null)
            {
                return null;
            }

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);
            return bitmap;
        }
        catch (Exception)
        {
            // Icons are best effort; items still show without one.
            return null;
        }
    }
}
