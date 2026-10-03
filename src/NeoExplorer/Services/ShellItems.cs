using System.Runtime.InteropServices;

namespace NeoExplorer.Services;

/// <summary>
/// Shell items (<c>IShellItem</c>), which the shell APIs for file operations, Open with and properties take.
/// </summary>
internal static class ShellItems
{
    // Bind handler for an IDataObject describing the item, as passed to apps that open it.
    public static readonly Guid BHID_DataObject = new("b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4");
    public static readonly Guid IID_IDataObject = new("0000010e-0000-0000-C000-000000000046");

    private const uint SIGDN_FILESYSPATH = 0x80058000;

    public static IShellItem Create(string path)
    {
        SHCreateItemFromParsingName(path, 0, typeof(IShellItem).GUID, out object item);
        return (IShellItem)item;
    }

    public static string? GetPath(IShellItem? item)
    {
        try
        {
            return item?.GetDisplayName(SIGDN_FILESYSPATH);
        }
        catch (COMException)
        {
            // Not a file system item.
            return null;
        }
    }

    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.FinalReleaseComObject(comObject);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string pszPath, nint pbc, in Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
}

[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    [return: MarshalAs(UnmanagedType.Interface)]
    object BindToHandler(nint pbc, in Guid bhid, in Guid riid);

    IShellItem GetParent();

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetDisplayName(uint sigdnName);

    uint GetAttributes(uint sfgaoMask);

    int Compare(IShellItem psi, uint hint);
}

/// <summary>
/// Runs shell COM calls on a thread of their own. Shell calls can pump window messages while they wait,
/// and doing that on the UI thread can re-enter XAML and crash (see <see cref="ShellIcons"/>). Calls that
/// show modal dialogs, such as file operations, would also block the UI thread for as long as they run.
/// </summary>
internal static class StaThread
{
    public static Task<T> Run<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                result.SetResult(action());
            }
            catch (Exception e)
            {
                result.SetException(e);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return result.Task;
    }

    public static Task Run(Action action) => Run(() =>
    {
        action();
        return true;
    });
}
