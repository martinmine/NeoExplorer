namespace NeoExplorer.Services;

public record DriveItem(string Name, string Path, long TotalSize, long FreeSpace);

public static class Drives
{
    /// <summary>
    /// The drives that are ready to use, named like File Explorer ("Local Disk (C:)").
    /// This can be slow for network drives; call it from a background thread.
    /// </summary>
    public static IReadOnlyList<DriveItem> GetDrives() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DriveItem(ShellInfo.GetDisplayName(d.Name), d.Name, d.TotalSize, d.TotalFreeSpace))
            .ToList();
}
