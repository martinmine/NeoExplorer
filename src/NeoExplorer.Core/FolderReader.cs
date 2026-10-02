using System.IO.Enumeration;

namespace NeoExplorer.Core;

/// <summary>
/// A file or folder in a directory listing. <see cref="Size"/> is null for folders.
/// </summary>
public record FileSystemItem(string Name, string Path, bool IsFolder, DateTime DateModified, long? Size, string Type);

public static class FolderReader
{
    private static readonly EnumerationOptions Options = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
    };

    private static readonly EnumerationOptions SearchOptions = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// Lists the files and folders in a directory, skipping hidden and system items like File Explorer does.
    /// <paramref name="getTypeName"/> provides the Type column text, e.g. "Text Document".
    /// This is blocking; call it from a background thread.
    /// </summary>
    public static IReadOnlyList<FileSystemItem> Read(string path, Func<FileSystemInfo, string> getTypeName, CancellationToken cancellationToken = default)
    {
        var items = new List<FileSystemItem>();
        foreach (FileSystemInfo info in new DirectoryInfo(path).EnumerateFileSystemInfos("*", Options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(ToItem(info, getTypeName));
        }

        return items;
    }

    /// <summary>
    /// Finds files and folders whose names match <paramref name="query"/> (see <see cref="NameFilter"/>)
    /// in a directory and all its subfolders. Results are produced as they are found, so a caller can show
    /// them while the search runs and stop early. This is blocking; enumerate it on a background thread.
    /// </summary>
    public static IEnumerable<FileSystemItem> Search(string path, string query, Func<FileSystemInfo, string> getTypeName, CancellationToken cancellationToken = default)
    {
        var matches = new FileSystemEnumerable<FileSystemInfo>(path, (ref FileSystemEntry entry) => entry.ToFileSystemInfo(), SearchOptions)
        {
            // Checked for every entry, so a search with few matches still stops quickly when cancelled.
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return NameFilter.Matches(entry.FileName, query);
            },

            // Don't follow links and junctions, which can loop back to a parent folder.
            ShouldRecursePredicate = (ref FileSystemEntry entry) => (entry.Attributes & FileAttributes.ReparsePoint) == 0,
        };

        return matches.Select(info => ToItem(info, getTypeName));
    }

    private static FileSystemItem ToItem(FileSystemInfo info, Func<FileSystemInfo, string> getTypeName)
    {
        long? size = info is FileInfo file ? file.Length : null;
        return new FileSystemItem(info.Name, info.FullName, size is null, info.LastWriteTime, size, getTypeName(info));
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
