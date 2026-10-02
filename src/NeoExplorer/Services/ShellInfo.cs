using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace NeoExplorer.Services;

/// <summary>
/// Names as shown by File Explorer, e.g. "Text Document" or "Local Disk (C:)".
/// </summary>
public static class ShellInfo
{
    // Type names are keyed by extension. Folders use a key that can never be an extension.
    private const string FolderKey = @"\";

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint SHGFI_DISPLAYNAME = 0x200;
    private const uint SHGFI_TYPENAME = 0x400;

    private static readonly ConcurrentDictionary<string, string> TypeNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The Type column text for a file or folder.
    /// </summary>
    public static string GetTypeName(FileSystemInfo info)
    {
        string key = info is DirectoryInfo ? FolderKey : info.Extension;
        return TypeNames.GetOrAdd(key, QueryTypeName);
    }

    /// <summary>
    /// The display name of an existing item, e.g. "Local Disk (C:)" for C:\. Falls back to the path.
    /// </summary>
    public static string GetDisplayName(string path)
    {
        var info = new SHFILEINFO();
        SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_DISPLAYNAME);
        return string.IsNullOrEmpty(info.szDisplayName) ? path : info.szDisplayName;
    }

    private static string QueryTypeName(string key)
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
