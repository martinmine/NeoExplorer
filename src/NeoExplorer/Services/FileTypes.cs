using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace NeoExplorer.Services;

/// <summary>
/// Type names as shown in File Explorer's Type column, e.g. "Text Document" or "File folder".
/// </summary>
public static class FileTypes
{
    // Keyed by extension. Folders use a key that can never be an extension.
    private const string FolderKey = @"\";

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint SHGFI_TYPENAME = 0x400;

    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string GetTypeName(FileSystemInfo info)
    {
        string key = info is DirectoryInfo ? FolderKey : info.Extension;
        return Cache.GetOrAdd(key, Query);
    }

    private static string Query(string key)
    {
        // With SHGFI_USEFILEATTRIBUTES the file doesn't need to exist; only the extension matters.
        bool isFolder = key == FolderKey;
        var info = new SHFILEINFO();
        SHGetFileInfo(
            isFolder ? "folder" : "file" + key,
            isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL,
            ref info,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES);
        return info.szTypeName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
}
