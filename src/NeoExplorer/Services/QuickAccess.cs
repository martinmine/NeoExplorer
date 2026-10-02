using System.Runtime.InteropServices;

namespace NeoExplorer.Services;

/// <summary>
/// A Quick Access folder. <see cref="Path"/> is a shell path such as "::{645FF040-...}" for locations
/// that aren't on disk, like the Recycle Bin.
/// </summary>
public record QuickAccessItem(string Name, string Path, bool IsPinned)
{
    public bool IsFileSystem => !Path.StartsWith("::", StringComparison.Ordinal);
}

public static class QuickAccess
{
    private const string QuickAccessFolder = "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}";

    /// <summary>
    /// The pinned and frequent folders, in File Explorer's order. Uses Shell COM, so call it from the UI thread.
    /// </summary>
    public static IReadOnlyList<QuickAccessItem> GetFolders()
    {
        var folders = new List<QuickAccessItem>();
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            dynamic items = shell.NameSpace(QuickAccessFolder).Items();
            for (int i = 0; i < items.Count; i++)
            {
                dynamic item = items.Item(i);
                string path = item.Path;

                // Quick Access also lists recent files, and the shell treats zip files as folders.
                if (!item.IsFolder || (item.IsFileSystem && !Directory.Exists(path)))
                {
                    continue;
                }

                folders.Add(new QuickAccessItem(item.Name, path, item.ExtendedProperty("System.Home.IsPinned") is true));
            }
        }
        catch (COMException)
        {
            // Show no Quick Access items rather than failing to start.
        }

        return folders;
    }
}
