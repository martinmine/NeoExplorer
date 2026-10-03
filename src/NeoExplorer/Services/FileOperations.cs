using System.Runtime.InteropServices;

namespace NeoExplorer.Services;

/// <summary>
/// Creates, copies, moves, renames and deletes through the shell (<c>IFileOperation</c>), like File Explorer does:
/// with its progress dialog, its "Replace or Skip Files" dialog, the Recycle Bin, and undo (Ctrl+Z) in Explorer.
/// Each operation runs on a thread of its own, so the window stays responsive while it runs.
/// </summary>
public static class FileOperations
{
    private const uint FOF_RENAMEONCOLLISION = 0x8;
    private const uint FOF_NOCONFIRMATION = 0x10;
    private const uint FOF_ALLOWUNDO = 0x40;
    private const uint FOF_WANTNUKEWARNING = 0x4000;
    private const uint FOFX_SHOWELEVATIONPROMPT = 0x40000;
    private const uint FOFX_RECYCLEONDELETE = 0x80000;
    private const uint FOFX_ADDUNDORECORD = 0x20000000;
    private const uint UndoableFlags = FOF_ALLOWUNDO | FOFX_ADDUNDORECORD | FOFX_SHOWELEVATIONPROMPT;

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    private const uint SSF_NOCONFIRMRECYCLE = 0x8000;

    // In SHELLFLAGSTATE, after fShowAllObjects and fShowExtensions.
    private const uint NoConfirmRecycleBit = 0x4;

    /// <summary>
    /// Copies items into a folder and returns the paths of the copies. Copying an item into its own folder
    /// makes a copy next to it, e.g. "notes - Copy.txt".
    /// </summary>
    public static Task<IReadOnlyList<string>> CopyAsync(IReadOnlyCollection<string> paths, string folder, nint owner)
    {
        bool sameFolder = paths.Any(p => string.Equals(Path.GetDirectoryName(p), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        return RunAsync(owner, UndoableFlags | (sameFolder ? FOF_RENAMEONCOLLISION : 0), (operation, destination) =>
        {
            foreach (string path in paths)
            {
                operation.CopyItem(ShellItems.Create(path), destination!, null, null);
            }
        }, folder);
    }

    /// <summary>
    /// Moves items into a folder and returns their new paths.
    /// </summary>
    public static Task<IReadOnlyList<string>> MoveAsync(IReadOnlyCollection<string> paths, string folder, nint owner) =>
        RunAsync(owner, UndoableFlags, (operation, destination) =>
        {
            foreach (string path in paths)
            {
                operation.MoveItem(ShellItems.Create(path), destination!, null, null);
            }
        }, folder);

    /// <summary>
    /// Sends items to the Recycle Bin, or deletes them for good. Deleting for good always asks first;
    /// for the Recycle Bin, Windows' "Display delete confirmation dialog" setting decides, as in File Explorer.
    /// </summary>
    public static Task DeleteAsync(IReadOnlyCollection<string> paths, bool permanently, nint owner)
    {
        uint flags = permanently
            ? FOFX_SHOWELEVATIONPROMPT
            : UndoableFlags | FOFX_RECYCLEONDELETE | FOF_WANTNUKEWARNING | (ConfirmsRecycle() ? 0 : FOF_NOCONFIRMATION);
        return RunAsync(owner, flags, (operation, _) =>
        {
            foreach (string path in paths)
            {
                operation.DeleteItem(ShellItems.Create(path), null);
            }
        });
    }

    /// <summary>
    /// Renames an item and returns its new path, or null if it wasn't renamed. Windows shows any error itself.
    /// </summary>
    public static async Task<string?> RenameAsync(string path, string newName, nint owner)
    {
        IReadOnlyList<string> results = await RunAsync(owner, UndoableFlags, (operation, _) =>
            operation.RenameItem(ShellItems.Create(path), newName, null));
        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>
    /// Creates an empty file or folder and returns its path, or null if it wasn't created. Windows shows any error itself.
    /// </summary>
    public static async Task<string?> NewItemAsync(string folder, string name, bool isFolder, nint owner)
    {
        uint attributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
        IReadOnlyList<string> results = await RunAsync(owner, UndoableFlags | FOF_RENAMEONCOLLISION, (operation, destination) =>
            operation.NewItem(destination!, attributes, name, null, null), folder);
        return results.Count > 0 ? results[0] : null;
    }

    private static Task<IReadOnlyList<string>> RunAsync(nint owner, uint flags, Action<IFileOperation, IShellItem?> queue, string? destinationFolder = null) =>
        StaThread.Run<IReadOnlyList<string>>(() =>
        {
            var operation = (IFileOperation)new FileOperation();
            var sink = new ProgressSink();
            try
            {
                operation.SetOwnerWindow(owner);
                operation.SetOperationFlags(flags);
                operation.Advise(sink, out _);
                queue(operation, destinationFolder is null ? null : ShellItems.Create(destinationFolder));
                operation.PerformOperations();
            }
            catch (COMException)
            {
                // Cancelled by the user, or failed. Windows has already shown its own message,
                // and whatever was done before that is still reported.
            }
            finally
            {
                ShellItems.Release(operation);
            }

            return sink.NewPaths;
        });

    private static bool ConfirmsRecycle()
    {
        SHGetSettings(out uint state, SSF_NOCONFIRMRECYCLE);
        return (state & NoConfirmRecycleBit) == 0;
    }

    [DllImport("shell32.dll")]
    private static extern void SHGetSettings(out uint lpsfs, uint dwMask);

    /// <summary>
    /// Collects where items ended up, so they can be selected afterwards.
    /// </summary>
    private sealed class ProgressSink : IFileOperationProgressSink
    {
        public List<string> NewPaths { get; } = [];

        public void PostRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName, int hrRename, IShellItem psiNewlyCreated) => Add(hrRename, psiNewlyCreated);

        public void PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, int hrMove, IShellItem psiNewlyCreated) => Add(hrMove, psiNewlyCreated);

        public void PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, int hrCopy, IShellItem psiNewlyCreated) => Add(hrCopy, psiNewlyCreated);

        public void PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName, string pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem psiNewItem) => Add(hrNew, psiNewItem);

        private void Add(int hresult, IShellItem? item)
        {
            if (hresult >= 0 && ShellItems.GetPath(item) is string path)
            {
                NewPaths.Add(path);
            }
        }

        public void StartOperations() { }
        public void FinishOperations(int hrResult) { }
        public void PreRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName) { }
        public void PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName) { }
        public void PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName) { }
        public void PreDeleteItem(uint dwFlags, IShellItem psiItem) { }
        public void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem psiNewlyCreated) { }
        public void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName) { }
        public void UpdateProgress(uint iWorkTotal, uint iWorkSoFar) { }
        public void ResetTimer() { }
        public void PauseTimer() { }
        public void ResumeTimer() { }
    }

    [ComImport]
    [Guid("3ad05575-8857-4850-9277-11b85bdb8e09")]
    private class FileOperation
    {
    }

    [ComImport]
    [Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IFileOperationProgressSink pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(uint dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog([MarshalAs(UnmanagedType.Interface)] object popd);
        void SetProperties([MarshalAs(UnmanagedType.Interface)] object pproparray);
        void SetOwnerWindow(nint hwndOwner);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IFileOperationProgressSink? pfopsItem);
        void RenameItems([MarshalAs(UnmanagedType.Interface)] object pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, IFileOperationProgressSink? pfopsItem);
        void MoveItems([MarshalAs(UnmanagedType.Interface)] object punkItems, IShellItem psiDestinationFolder);
        void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszCopyName, IFileOperationProgressSink? pfopsItem);
        void CopyItems([MarshalAs(UnmanagedType.Interface)] object punkItems, IShellItem psiDestinationFolder);
        void DeleteItem(IShellItem psiItem, IFileOperationProgressSink? pfopsItem);
        void DeleteItems([MarshalAs(UnmanagedType.Interface)] object punkItems);
        void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, IFileOperationProgressSink? pfopsItem);
        void PerformOperations();
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetAnyOperationsAborted();
    }
}

[ComImport]
[Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperationProgressSink
{
    void StartOperations();
    void FinishOperations(int hrResult);
    void PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    void PostRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, int hrRename, IShellItem psiNewlyCreated);
    void PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    void PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, int hrMove, IShellItem psiNewlyCreated);
    void PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    void PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, int hrCopy, IShellItem psiNewlyCreated);
    void PreDeleteItem(uint dwFlags, IShellItem psiItem);
    void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem psiNewlyCreated);
    void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    void PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem psiNewItem);
    void UpdateProgress(uint iWorkTotal, uint iWorkSoFar);
    void ResetTimer();
    void PauseTimer();
    void ResumeTimer();
}
