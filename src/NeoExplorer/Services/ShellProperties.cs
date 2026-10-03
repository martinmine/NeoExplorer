using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NeoExplorer.Services;

/// <summary>
/// A line on the Details tab: a group heading such as "Description", or a property and its value.
/// </summary>
public sealed record PropertyRow(string Name, string Value, bool IsGroup)
{
    public bool IsProperty => !IsGroup;
}

/// <summary>
/// File details from the Windows property system, as File Explorer shows them in Properties.
/// </summary>
public static class ShellProperties
{
    // System.PropList.FullDetails: the properties a file type lists on its Details tab.
    private static readonly PROPERTYKEY FullDetails = new(new Guid("C9944A21-A406-48FE-8225-AEC7E24C211B"), 2);

    private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
    private static readonly Guid IID_IPropertyDescriptionList = new("1f9fc1d0-c39b-4b26-817f-011967d3440e");
    private static readonly Guid IID_IPropertyDescription = new("6f79d558-3e96-4549-a1d1-7d75d2288814");

    private const int GPS_BESTEFFORT = 0x40;
    private const uint PDTF_ISGROUP = 0x4;
    private const int PDFF_DEFAULT = 0;

    /// <summary>
    /// The groups and properties for the Details tab, with values formatted as Windows formats them.
    /// Empty values are included, as File Explorer lists them too. Returns an empty list if they can't be read.
    /// </summary>
    public static Task<IReadOnlyList<PropertyRow>> GetDetailsAsync(string path) => StaThread.Run<IReadOnlyList<PropertyRow>>(() =>
    {
        var rows = new List<PropertyRow>();
        IShellItem2? item = null;
        object? store = null;
        IPropertyDescriptionList? list = null;
        try
        {
            item = (IShellItem2)ShellItems.Create(path);
            store = item.GetPropertyStore(GPS_BESTEFFORT, IID_IPropertyStore);
            list = (IPropertyDescriptionList)item.GetPropertyDescriptionList(FullDetails, IID_IPropertyDescriptionList);
            uint count = list.GetCount();
            for (uint i = 0; i < count; i++)
            {
                var description = (IPropertyDescription)list.GetAt(i, IID_IPropertyDescription);
                try
                {
                    string name = description.GetDisplayName();
                    if (description.GetTypeFlags(PDTF_ISGROUP) != 0)
                    {
                        rows.Add(new PropertyRow(name, "", IsGroup: true));
                    }
                    else
                    {
                        string value = PSFormatPropertyValue(store, description, PDFF_DEFAULT, out string formatted) >= 0 ? formatted : "";
                        rows.Add(new PropertyRow(name, value, IsGroup: false));
                    }
                }
                catch (COMException)
                {
                    // A property without a display name; File Explorer leaves those out too.
                }
                finally
                {
                    ShellItems.Release(description);
                }
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            // No property handler for this item.
        }
        finally
        {
            ShellItems.Release(list);
            ShellItems.Release(store);
            ShellItems.Release(item);
        }

        // Drop headings that ended up with nothing under them.
        return rows.Where((row, index) => !row.IsGroup || (index + 1 < rows.Count && !rows[index + 1].IsGroup)).ToList();
    });

    /// <summary>
    /// The space a file takes on disk, as the file system reports it: whole clusters, less for compressed or
    /// sparse files, and nothing for small files stored inside the file table. Null if it can't be read.
    /// </summary>
    public static long? GetAllocationSize(string path)
    {
        const uint FILE_READ_ATTRIBUTES = 0x80;
        const uint ShareAll = 0x7;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        const int FileStandardInfo = 1;

        using SafeFileHandle handle = CreateFile(path, FILE_READ_ATTRIBUTES, ShareAll, 0, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, 0);
        return !handle.IsInvalid && GetFileInformationByHandleEx(handle, FileStandardInfo, out FILE_STANDARD_INFO info, Marshal.SizeOf<FILE_STANDARD_INFO>())
            ? info.AllocationSize
            : null;
    }

    /// <summary>
    /// The cluster size of the drive a path is on, used to work out the size on disk of a folder's files.
    /// </summary>
    public static long GetClusterSize(string path)
    {
        string? root = Path.GetPathRoot(path);
        return root is not null && GetDiskFreeSpace(root, out uint sectorsPerCluster, out uint bytesPerSector, out _, out _)
            ? (long)sectorsPerCluster * bytesPerSector
            : 4096;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct PROPERTYKEY(Guid FormatId, uint PropertyId);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_STANDARD_INFO
    {
        public long AllocationSize;
        public long EndOfFile;
        public uint NumberOfLinks;
        public byte DeletePending;
        public byte Directory;
    }

    [DllImport("propsys.dll", CharSet = CharSet.Unicode)]
    private static extern int PSFormatPropertyValue(
        [MarshalAs(UnmanagedType.Interface)] object pps,
        IPropertyDescription ppd,
        int pdff,
        [MarshalAs(UnmanagedType.LPWStr)] out string ppszDisplay);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle hFile, int infoClass, out FILE_STANDARD_INFO info, int size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDiskFreeSpace(string lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

    [ComImport]
    [Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem2
    {
        // IShellItem
        [return: MarshalAs(UnmanagedType.Interface)]
        object BindToHandler(nint pbc, in Guid bhid, in Guid riid);
        IShellItem GetParent();
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName(uint sigdnName);
        uint GetAttributes(uint sfgaoMask);
        int Compare(IShellItem psi, uint hint);

        // IShellItem2, up to the methods used here.
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetPropertyStore(int flags, in Guid riid);
        void GetPropertyStoreWithCreateObject();
        void GetPropertyStoreForKeys();
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetPropertyDescriptionList(in PROPERTYKEY keyType, in Guid riid);
    }

    [ComImport]
    [Guid("1f9fc1d0-c39b-4b26-817f-011967d3440e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyDescriptionList
    {
        uint GetCount();
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetAt(uint iElem, in Guid riid);
    }

    [ComImport]
    [Guid("6f79d558-3e96-4549-a1d1-7d75d2288814")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyDescription
    {
        PROPERTYKEY GetPropertyKey();
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetCanonicalName();
        ushort GetPropertyType();
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName();
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetEditInvitation();
        uint GetTypeFlags(uint mask);
    }
}
