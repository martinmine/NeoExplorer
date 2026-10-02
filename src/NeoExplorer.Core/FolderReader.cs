namespace NeoExplorer.Core;

/// <summary>
/// A file or folder in a directory listing. <see cref="Size"/> is null for folders.
/// </summary>
public record FileSystemItem(string Name, string Path, bool IsFolder, DateTime DateModified, long? Size);

public static class FolderReader
{
    private static readonly EnumerationOptions Options = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
    };

    /// <summary>
    /// Lists the files and folders in a directory, skipping hidden and system items like File Explorer does.
    /// This is blocking; call it from a background thread.
    /// </summary>
    public static IReadOnlyList<FileSystemItem> Read(string path, CancellationToken cancellationToken = default)
    {
        var items = new List<FileSystemItem>();
        foreach (FileSystemInfo info in new DirectoryInfo(path).EnumerateFileSystemInfos("*", Options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            items.Add(info is FileInfo file
                ? new FileSystemItem(file.Name, file.FullName, false, file.LastWriteTime, file.Length)
                : new FileSystemItem(info.Name, info.FullName, true, info.LastWriteTime, null));
        }

        return items;
    }
}
