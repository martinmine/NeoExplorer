namespace NeoExplorer.Core;

/// <summary>
/// Sets and clears the Read-only and Hidden attributes, as the Properties window does.
/// </summary>
public static class AttributeChanger
{
    private static readonly EnumerationOptions AllItems = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// Applies the change to <paramref name="path"/>, and with <paramref name="includeContents"/> also to every
    /// file and subfolder in it, like "Apply changes to this folder, subfolders and files". Items that can't be
    /// changed are skipped and counted. This is blocking; call it from a background thread.
    /// </summary>
    /// <returns>How many items couldn't be changed.</returns>
    public static int Apply(string path, FileAttributes set, FileAttributes clear, bool includeContents, CancellationToken cancellationToken = default)
    {
        int failed = TryApply(path, set, clear) ? 0 : 1;
        if (includeContents && Directory.Exists(path))
        {
            foreach (string item in Directory.EnumerateFileSystemEntries(path, "*", AllItems))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryApply(item, set, clear))
                {
                    failed++;
                }
            }
        }

        return failed;
    }

    public static FileAttributes Change(FileAttributes attributes, FileAttributes set, FileAttributes clear) => (attributes | set) & ~clear;

    private static bool TryApply(string path, FileAttributes set, FileAttributes clear)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            FileAttributes changed = Change(attributes, set, clear);
            if (changed != attributes)
            {
                File.SetAttributes(path, changed);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
