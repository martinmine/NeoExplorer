using System.Runtime.InteropServices;

namespace NeoExplorer.Services;

public record KnownFolderItem(string Name, string Path);

public static class KnownFolders
{
    // In the order File Explorer shows them on This PC.
    private static readonly Guid[] ThisPcFolderIds =
    [
        new("FDD39AD0-238F-46AF-ADB4-6C85480369C7"), // Documents
        new("374DE290-123F-4565-9164-39C4925E467B"), // Downloads
        new("4BD8D571-6D19-48D3-BE97-422220080E43"), // Music
        new("33E28130-4E1E-4676-835A-98395C3BC3BB"), // Pictures
        new("18989B1D-99B5-455B-841C-AB7C74E4DDFC"), // Videos
        new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641"), // Desktop
    ];

    /// <summary>
    /// The user folders File Explorer lists on This PC, with their localized names. Skips folders that don't exist.
    /// </summary>
    public static IReadOnlyList<KnownFolderItem> GetThisPcFolders()
    {
        var folders = new List<KnownFolderItem>();
        foreach (Guid id in ThisPcFolderIds)
        {
            if (SHGetKnownFolderPath(id, 0, 0, out string path) == 0 && Directory.Exists(path))
            {
                folders.Add(new KnownFolderItem(ShellInfo.GetDisplayName(path), path));
            }
        }

        return folders;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, nint hToken, out string ppszPath);
}
