namespace NeoExplorer.Core;

public static class FileNameValidator
{
    /// <summary>
    /// The message File Explorer shows when one of these is typed in a name.
    /// </summary>
    public const string InvalidCharactersMessage = "A file name can't contain any of the following characters:\n\\ / : * ? \" < > |";

    private const int MaxNameLength = 255;

    private static readonly char[] InvalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    // Device names that Windows reserves, with or without an extension (e.g. "con.txt").
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static bool ContainsInvalidCharacters(string name) =>
        name.AsSpan().IndexOfAny(InvalidChars) >= 0 || name.Any(char.IsControl);

    /// <summary>
    /// Trims what Windows would drop anyway: spaces around the name and dots at the end.
    /// </summary>
    public static string Normalize(string name) => name.Trim().TrimEnd('.', ' ');

    /// <summary>
    /// Returns a message for the user if <paramref name="name"/> (already normalized) can't be used, or null if it can.
    /// </summary>
    public static string? Validate(string name)
    {
        if (name.Length == 0)
        {
            return "You must type a file name.";
        }

        if (ContainsInvalidCharacters(name))
        {
            return InvalidCharactersMessage;
        }

        if (name.Length > MaxNameLength)
        {
            return "The file name is too long.";
        }

        int dot = name.IndexOf('.');
        string baseName = (dot < 0 ? name : name[..dot]).TrimEnd();
        if (ReservedNames.Contains(baseName))
        {
            return "The specified device name is invalid.";
        }

        return null;
    }

    /// <summary>
    /// How many characters to select when renaming starts: the name without its extension, as File Explorer does.
    /// Folders, and names like ".gitignore" that are only an extension, are selected whole.
    /// </summary>
    public static int RenameSelectionLength(string name, bool isFolder)
    {
        int dot = name.LastIndexOf('.');
        return isFolder || dot <= 0 ? name.Length : dot;
    }

    /// <summary>
    /// Whether a rename changes the file's extension, which File Explorer asks about first.
    /// </summary>
    public static bool ChangesExtension(string oldName, string newName) =>
        !string.Equals(Path.GetExtension(oldName), Path.GetExtension(newName), StringComparison.OrdinalIgnoreCase);
}
