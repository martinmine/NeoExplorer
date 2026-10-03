namespace NeoExplorer.Core;

public enum DropOperation
{
    None,
    Copy,
    Move,
}

/// <summary>
/// What dropping or pasting files into a folder does, following File Explorer's rules.
/// </summary>
public static class DropEffects
{
    /// <summary>
    /// Chooses copy or move for dropping <paramref name="sources"/> into <paramref name="targetFolder"/>:
    /// Shift moves, Ctrl copies, otherwise items move within a drive and copy to another drive.
    /// Returns <see cref="DropOperation.None"/> when the drop can't do anything useful.
    /// </summary>
    public static DropOperation Choose(IReadOnlyCollection<string> sources, string targetFolder, bool control, bool shift)
    {
        if (!CanCopy(sources, targetFolder))
        {
            return DropOperation.None;
        }

        DropOperation operation = shift ? DropOperation.Move
            : control ? DropOperation.Copy
            : sources.All(s => IsSameDrive(s, targetFolder)) ? DropOperation.Move
            : DropOperation.Copy;

        // Moving items to the folder they are already in does nothing. Copying there makes "- Copy" duplicates.
        return operation == DropOperation.Move && !CanMove(sources, targetFolder) ? DropOperation.None : operation;
    }

    /// <summary>
    /// Whether pasting cut items into <paramref name="targetFolder"/> would do anything.
    /// Cut items pasted into their own folder stay where they are, as in File Explorer.
    /// </summary>
    public static bool CanMove(IReadOnlyCollection<string> sources, string targetFolder) =>
        sources.Count > 0 && !sources.Any(s => IsSameOrInside(targetFolder, s)) && !sources.All(s => IsInFolder(s, targetFolder));

    /// <summary>
    /// Whether copying into <paramref name="targetFolder"/> is possible: a folder can't be copied into itself.
    /// </summary>
    public static bool CanCopy(IReadOnlyCollection<string> sources, string targetFolder) =>
        sources.Count > 0 && !sources.Any(s => IsSameOrInside(targetFolder, s));

    /// <summary>
    /// Whether <paramref name="path"/> is <paramref name="folder"/> or somewhere inside it.
    /// </summary>
    public static bool IsSameOrInside(string path, string folder)
    {
        string a = Trim(path);
        string b = Trim(folder);
        return a.Equals(b, StringComparison.OrdinalIgnoreCase)
            || (a.Length > b.Length && a.StartsWith(b, StringComparison.OrdinalIgnoreCase) && a[b.Length] == Path.DirectorySeparatorChar)
            || (b.EndsWith(Path.DirectorySeparatorChar) && a.StartsWith(b, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether two paths name the same folder, ignoring case and a trailing separator.
    /// </summary>
    public static bool IsSamePath(string? a, string? b) =>
        a is not null && b is not null && Trim(a).Equals(Trim(b), StringComparison.OrdinalIgnoreCase);

    private static bool IsInFolder(string path, string folder) => IsSamePath(Path.GetDirectoryName(Trim(path)), folder);

    private static bool IsSameDrive(string a, string b) =>
        string.Equals(Path.GetPathRoot(a), Path.GetPathRoot(b), StringComparison.OrdinalIgnoreCase);

    // Keeps the separator of a drive root such as C:\, which is part of its name.
    private static string Trim(string path) =>
        path.Length > 3 ? path.TrimEnd(Path.DirectorySeparatorChar) : path;
}
