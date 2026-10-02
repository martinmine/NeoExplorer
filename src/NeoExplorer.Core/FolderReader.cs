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

    /// <summary>
    /// Returns the folder's full path with the casing used on disk (e.g. c:\windows becomes C:\Windows),
    /// or null if the folder does not exist.
    /// </summary>
    public static string? FindFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return null;
        }

        string root = Path.GetPathRoot(path)!;
        string result = root.Length >= 2 && root[1] == ':' ? char.ToUpperInvariant(root[0]) + root[1..] : root;
        foreach (string part in path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            string? match = null;
            try
            {
                match = Directory.EnumerateDirectories(result, part).FirstOrDefault();
            }
            catch (UnauthorizedAccessException)
            {
                // Can't list the parent; keep the casing as typed.
            }

            result = match ?? Path.Combine(result, part);
        }

        return result;
    }
}
