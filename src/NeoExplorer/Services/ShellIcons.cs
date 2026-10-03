using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NeoExplorer.Services;

public static class ShellIcons
{
    private const int SIIGBF_ICONONLY = 0x4;

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

    /// <summary>
    /// Loads the icon of a shell location that isn't on disk, such as "::{20D04FE0-...}" for This PC.
    /// The size is in physical pixels. Returns null if Windows can't provide one. Must be called on the UI thread.
    /// </summary>
    public static ImageSource? LoadShellLocation(string parsingName, int size)
    {
        nint hbitmap = 0;
        try
        {
            SHCreateItemFromParsingName(parsingName, 0, typeof(IShellItemImageFactory).GUID, out IShellItemImageFactory factory);
            factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY, out hbitmap);
            return ToWriteableBitmap(hbitmap);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (hbitmap != 0)
            {
                DeleteObject(hbitmap);
            }
        }
    }

    private static WriteableBitmap? ToWriteableBitmap(nint hbitmap)
    {
        if (GetObject(hbitmap, Marshal.SizeOf<BITMAP>(), out BITMAP info) == 0)
        {
            return null;
        }

        // Negative height asks for rows top-down, as WriteableBitmap expects. The shell gives premultiplied BGRA.
        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = info.bmWidth,
            biHeight = -info.bmHeight,
            biPlanes = 1,
            biBitCount = 32,
        };
        byte[] pixels = new byte[info.bmWidth * info.bmHeight * 4];
        nint dc = GetDC(0);
        try
        {
            if (GetDIBits(dc, hbitmap, 0, (uint)info.bmHeight, pixels, ref header, 0) == 0)
            {
                return null;
            }
        }
        finally
        {
            ReleaseDC(0, dc);
        }

        var bitmap = new WriteableBitmap(info.bmWidth, info.bmHeight);
        using (Stream stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixels);
        }
        bitmap.Invalidate();
        return bitmap;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(SIZE size, int flags, out nint phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string pszPath, nint pbc, in Guid riid, out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(nint h, int c, out BITMAP pv);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint hdc, nint hbm, uint start, uint cLines, byte[] lpvBits, ref BITMAPINFOHEADER lpbmi, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint ho);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);
}
