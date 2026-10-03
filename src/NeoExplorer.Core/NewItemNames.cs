namespace NeoExplorer.Core;

/// <summary>
/// Names for items made with "New", the way File Explorer picks them: "New folder", then "New folder (2)", and so on.
/// </summary>
public static class NewItemNames
{
    public const string Folder = "New folder";

    public const string TextDocument = "New Text Document.txt";

    /// <summary>
    /// Returns <paramref name="name"/>, or the first numbered variant of it, starting at (2), that
    /// <paramref name="exists"/> says isn't taken. The number goes before the extension.
    /// </summary>
    public static string Unique(string name, bool isFolder, Func<string, bool> exists)
    {
        // Folders have no extension, even when the name has a dot in it.
        string extension = isFolder ? "" : Path.GetExtension(name);
        string baseName = name[..^extension.Length];
        string candidate = name;
        for (int number = 2; exists(candidate); number++)
        {
            candidate = $"{baseName} ({number}){extension}";
        }

        return candidate;
    }
}
