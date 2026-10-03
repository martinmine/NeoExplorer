using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NeoExplorer.Services;

/// <summary>
/// An app that can open a file type, as listed under "Open with".
/// </summary>
/// <param name="Id">Identifies the app to <see cref="ShellAssociations.OpenWithAsync"/>.</param>
/// <param name="IconReference">Where its icon is: "C:\path\app.exe,0", or "@{...}" for a Store app.</param>
public sealed record AppInfo(string Id, string Name, string? IconReference);

/// <summary>
/// Which apps open which file types: the default app, and the apps offered under "Open with".
/// </summary>
public static class ShellAssociations
{
    private const uint ASSOCF_NOTRUNCATE = 0x20;
    private const uint ASSOCF_INIT_IGNOREUNKNOWN = 0x400;
    private const int ASSOCSTR_FRIENDLYAPPNAME = 4;
    private const int ASSOCSTR_APPICONREFERENCE = 23;
    private const int ASSOC_FILTER_RECOMMENDED = 1;

    private const int OAIF_ALLOW_REGISTRATION = 0x1;
    private const int OAIF_REGISTER_EXT = 0x2;
    private const int OAIF_EXEC = 0x4;
    private const int OAIF_FORCE_REGISTRATION = 0x8;

    /// <summary>
    /// The app that opens files with this extension (e.g. ".txt") when they are double-clicked,
    /// or null if Windows would ask which app to use.
    /// </summary>
    public static AppInfo? GetDefaultApp(string extension)
    {
        if (extension.Length == 0 || QueryString(ASSOCSTR_FRIENDLYAPPNAME, extension) is not string name)
        {
            return null;
        }

        return new AppInfo("", name, QueryString(ASSOCSTR_APPICONREFERENCE, extension));
    }

    /// <summary>
    /// The apps Windows recommends for this extension, as shown in File Explorer's "Open with" menu.
    /// </summary>
    public static Task<IReadOnlyList<AppInfo>> GetOpenWithAppsAsync(string extension) =>
        StaThread.Run<IReadOnlyList<AppInfo>>(() =>
        {
            var apps = new List<AppInfo>();
            foreach (IAssocHandler handler in EnumerateHandlers(extension))
            {
                try
                {
                    string? icon = null;
                    try
                    {
                        handler.GetIconLocation(out string path, out int index);
                        icon = $"{path},{index}";
                    }
                    catch (COMException)
                    {
                        // No icon; the menu shows the app without one.
                    }

                    apps.Add(new AppInfo(handler.GetName(), handler.GetUIName(), icon));
                }
                catch (COMException)
                {
                    // Skip apps whose details can't be read.
                }
                finally
                {
                    ShellItems.Release(handler);
                }
            }

            return apps.DistinctBy(a => a.Name).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        });

    /// <summary>
    /// Opens a file with one of the apps from <see cref="GetOpenWithAppsAsync"/>.
    /// </summary>
    public static Task OpenWithAsync(string path, AppInfo app) => StaThread.Run(() =>
    {
        foreach (IAssocHandler handler in EnumerateHandlers(Path.GetExtension(path)))
        {
            try
            {
                if (handler.GetName() == app.Id)
                {
                    object dataObject = ShellItems.Create(path).BindToHandler(0, ShellItems.BHID_DataObject, ShellItems.IID_IDataObject);
                    handler.Invoke(dataObject);
                    return;
                }
            }
            catch (COMException)
            {
                // The app failed to start; Windows usually shows why.
                return;
            }
            finally
            {
                ShellItems.Release(handler);
            }
        }
    });

    /// <summary>
    /// Shows Windows' "How do you want to open this file?" dialog. With <paramref name="changeDefault"/>,
    /// the chosen app becomes the default for the file type (Properties > Change...), without opening the file.
    /// Otherwise the file is opened, and the dialog offers "Always" (Open with > Choose another app).
    /// </summary>
    public static Task ShowOpenWithDialogAsync(string path, bool changeDefault, nint owner) => StaThread.Run(() =>
    {
        var info = new OPENASINFO
        {
            pcszFile = path,
            oaifInFlags = changeDefault
                ? OAIF_ALLOW_REGISTRATION | OAIF_REGISTER_EXT | OAIF_FORCE_REGISTRATION
                : OAIF_ALLOW_REGISTRATION | OAIF_REGISTER_EXT | OAIF_EXEC,
        };

        // Fails when cancelled, which needs no message.
        _ = SHOpenWithDialog(owner, ref info);
    });

    /// <summary>
    /// Loads an app icon from an <see cref="AppInfo.IconReference"/>. Returns null if it can't be loaded.
    /// Must be called on the UI thread.
    /// </summary>
    public static async Task<ImageSource?> LoadIconAsync(string? iconReference, uint size)
    {
        if (string.IsNullOrEmpty(iconReference))
        {
            return null;
        }

        if (iconReference.StartsWith('@'))
        {
            // A Store app's logo, given as a resource reference that resolves to an image file in its package.
            string reference = iconReference[..LastComma(iconReference)];
            string? file = await Task.Run(() => LoadIndirectString(reference));
            return file is not null && File.Exists(file) ? new BitmapImage(new Uri(file)) : null;
        }

        // "C:\path\app.exe,0". Shows the app's main icon, as the shell does for an exe.
        string exe = Environment.ExpandEnvironmentVariables(iconReference[..LastComma(iconReference)].Trim('"'));
        return File.Exists(exe) ? await ShellIcons.LoadAsync(exe, isFolder: false, size) : null;
    }

    private static int LastComma(string reference)
    {
        int comma = reference.LastIndexOf(',');
        return comma > 0 && int.TryParse(reference.AsSpan(comma + 1), out _) ? comma : reference.Length;
    }

    private static IEnumerable<IAssocHandler> EnumerateHandlers(string extension)
    {
        if (extension.Length == 0 || SHAssocEnumHandlers(extension, ASSOC_FILTER_RECOMMENDED, out IEnumAssocHandlers handlers) < 0)
        {
            yield break;
        }

        try
        {
            var batch = new IAssocHandler[1];
            while (handlers.Next(1, batch, out uint fetched) == 0 && fetched == 1)
            {
                yield return batch[0];
            }
        }
        finally
        {
            ShellItems.Release(handlers);
        }
    }

    private static string? QueryString(int key, string extension)
    {
        uint length = 0;
        if (AssocQueryString(ASSOCF_NOTRUNCATE | ASSOCF_INIT_IGNOREUNKNOWN, key, extension, null, null, ref length) != 1 || length == 0)
        {
            return null;
        }

        var buffer = new StringBuilder((int)length);
        return AssocQueryString(ASSOCF_NOTRUNCATE | ASSOCF_INIT_IGNOREUNKNOWN, key, extension, null, buffer, ref length) == 0
            ? buffer.ToString()
            : null;
    }

    private static string? LoadIndirectString(string reference)
    {
        var buffer = new StringBuilder(1024);
        return SHLoadIndirectString(reference, buffer, buffer.Capacity, 0) == 0 ? buffer.ToString() : null;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, int str, string pszAssoc, string? pszExtra, StringBuilder? pszOut, ref uint pcchOut);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, nint ppvReserved);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHAssocEnumHandlers(string pszExtra, int afFilter, out IEnumAssocHandlers ppEnumHandler);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHOpenWithDialog(nint hwndParent, ref OPENASINFO poainfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENASINFO
    {
        public string pcszFile;
        public string? pcszClass;
        public int oaifInFlags;
    }

    [ComImport]
    [Guid("973810ae-9599-4b88-9e4d-6ee98c9552da")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumAssocHandlers
    {
        [PreserveSig]
        int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IAssocHandler[] rgelt, out uint pceltFetched);
    }

    [ComImport]
    [Guid("F04061AC-1659-4a3f-A954-775AA57FC083")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAssocHandler
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetName();

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetUIName();

        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] out string ppszPath, out int pIndex);

        [PreserveSig]
        int IsRecommended();

        void MakeDefault([MarshalAs(UnmanagedType.LPWStr)] string pszDescription);

        void Invoke([MarshalAs(UnmanagedType.Interface)] object pdo);
    }
}
