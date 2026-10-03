using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace NeoExplorer.Services;

/// <summary>
/// Cut, copy and paste of files, in the clipboard formats File Explorer uses, so they work between the two.
/// Must be used on the UI thread.
/// </summary>
public static class ShellClipboard
{
    private static HashSet<string> _cutPaths = new(StringComparer.OrdinalIgnoreCase);
    private static uint _cutSequence;
    private static bool _listening;

    /// <summary>
    /// Raised when <see cref="IsCut"/> changes for some items, so they can be shown faded or normal again.
    /// </summary>
    public static event EventHandler? CutChanged;

    /// <summary>
    /// Whether an item was cut in NeoExplorer and is still waiting to be pasted.
    /// </summary>
    public static bool IsCut(string path) => _cutPaths.Contains(path);

    public static async Task SetAsync(IReadOnlyCollection<string> paths, bool cut)
    {
        IReadOnlyList<IStorageItem> items = await GetStorageItemsAsync(paths);
        if (items.Count == 0)
        {
            return;
        }

        var package = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
        package.SetStorageItems(items, readOnly: false);
        Clipboard.SetContent(package);

        // Keeps the files on the clipboard after NeoExplorer closes, like File Explorer.
        Clipboard.Flush();

        ListenForChanges();
        _cutPaths = cut ? new(paths, StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
        _cutSequence = GetClipboardSequenceNumber();
        CutChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Whether the clipboard holds files or folders, from NeoExplorer, File Explorer or another app.
    /// </summary>
    public static bool HasFiles()
    {
        try
        {
            return Clipboard.GetContent().Contains(StandardDataFormats.StorageItems);
        }
        catch (COMException)
        {
            // Another app has the clipboard open.
            return false;
        }
    }

    /// <summary>
    /// The files on the clipboard, and whether they were cut rather than copied. Null if there are none.
    /// </summary>
    public static async Task<(IReadOnlyList<string> Paths, bool Cut)?> GetAsync()
    {
        try
        {
            DataPackageView content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.StorageItems))
            {
                return null;
            }

            IReadOnlyList<IStorageItem> items = await content.GetStorageItemsAsync();
            List<string> paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
            return paths.Count == 0 ? null : (paths, content.RequestedOperation.HasFlag(DataPackageOperation.Move));
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cut files can be pasted only once: afterwards the clipboard is emptied, as in File Explorer.
    /// </summary>
    public static void ClearAfterMove()
    {
        Clipboard.Clear();
        ClearCut();
    }

    /// <summary>
    /// Creates storage items for paths that still exist. The Storage calls go through COM, so they run on a
    /// background thread (see <see cref="ShellIcons"/>).
    /// </summary>
    public static Task<IReadOnlyList<IStorageItem>> GetStorageItemsAsync(IEnumerable<string> paths) =>
        Task.Run<IReadOnlyList<IStorageItem>>(async () =>
        {
            var items = new List<IStorageItem>();
            foreach (string path in paths)
            {
                try
                {
                    items.Add(Directory.Exists(path)
                        ? await StorageFolder.GetFolderFromPathAsync(path)
                        : await StorageFile.GetFileFromPathAsync(path));
                }
                catch (Exception e) when (e is FileNotFoundException or UnauthorizedAccessException or ArgumentException)
                {
                    // Deleted in the meantime, or not accessible; leave it out.
                }
            }

            return items;
        });

    private static void ListenForChanges()
    {
        if (!_listening)
        {
            _listening = true;
            Clipboard.ContentChanged += (_, _) =>
            {
                // Something else was put on the clipboard, so the cut items won't be moved anymore.
                if (_cutPaths.Count > 0 && GetClipboardSequenceNumber() != _cutSequence)
                {
                    ClearCut();
                }
            };
        }
    }

    private static void ClearCut()
    {
        if (_cutPaths.Count > 0)
        {
            _cutPaths = new(StringComparer.OrdinalIgnoreCase);
            CutChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
