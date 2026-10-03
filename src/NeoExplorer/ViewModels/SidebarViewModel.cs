using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

public partial class SidebarItem(string name, string path, bool isPinned = false, string glyph = "") : ObservableObject
{
    private const string ThisPCParsingName = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    public string Name => name;

    /// <summary>
    /// A folder path, This PC, or a shell path such as "::{645FF040-...}" for the Recycle Bin.
    /// </summary>
    public string Path => path;

    public bool IsPinned => isPinned;

    /// <summary>
    /// The name read by screen readers, which can't see the pin icon.
    /// </summary>
    public string AccessibleName => isPinned ? $"{name}, pinned" : name;

    /// <summary>
    /// Shown instead of a shell icon for locations that aren't folders on disk.
    /// </summary>
    public string Glyph => glyph;

    public bool IsFileSystem => !path.StartsWith("::", StringComparison.Ordinal);

    [ObservableProperty]
    public partial IReadOnlyList<SidebarItem> Children { get; set; } = [];

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    public async Task LoadIconAsync()
    {
        if (path == PathParser.ThisPC)
        {
            // 32 px stays sharp at up to 200% display scaling.
            Icon = ShellIcons.LoadShellLocation(ThisPCParsingName, 32);
        }
        else if (glyph == "")
        {
            Icon = await ShellIcons.LoadAsync(path, isFolder: true, 16);
        }
    }

    // Used by UI Automation (screen readers) as the item's name.
    public override string ToString() => Name;
}

/// <summary>
/// The navigation pane: Quick Access folders, then This PC with its drives.
/// </summary>
public partial class SidebarViewModel(Action<string> navigate) : ObservableObject
{
    private const string RecycleBinPath = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    private const string RecycleBinGlyph = "";
    private const string ShellFolderGlyph = "";

    [ObservableProperty]
    public partial IReadOnlyList<SidebarItem> QuickAccessItems { get; private set; } = [];

    public SidebarItem ThisPC { get; } = new(PathParser.ThisPC, PathParser.ThisPC);

    /// <summary>
    /// This PC as a one-item list, for the tree that shows it.
    /// </summary>
    public IReadOnlyList<SidebarItem> ThisPCItems => [ThisPC];

    public async Task LoadAsync()
    {
        QuickAccessItems = QuickAccess.GetFolders()
            .Select(f => new SidebarItem(f.Name, f.Path, f.IsPinned, f.IsFileSystem ? "" : f.Path == RecycleBinPath ? RecycleBinGlyph : ShellFolderGlyph))
            .ToList();

        IReadOnlyList<DriveItem> drives = await Task.Run(Drives.GetDrives);
        ThisPC.Children = drives.Select(d => new SidebarItem(d.Name, d.Path)).ToList();

        foreach (SidebarItem item in QuickAccessItems.Concat(ThisPCItems).Concat(ThisPC.Children))
        {
            _ = item.LoadIconAsync();
        }
    }

    /// <summary>
    /// Opens a folder in NeoExplorer. Shell locations such as the Recycle Bin open in File Explorer.
    /// </summary>
    public void Open(SidebarItem item)
    {
        if (item.IsFileSystem)
        {
            navigate(item.Path);
        }
        else
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "shell:" + item.Path) { UseShellExecute = true });
        }
    }

    /// <summary>
    /// Finds the item for a location, so the sidebar can highlight where the user is.
    /// </summary>
    public SidebarItem? Find(string location) =>
        QuickAccessItems.Concat(ThisPCItems).Concat(ThisPC.Children)
            .FirstOrDefault(i => string.Equals(i.Path, location, StringComparison.OrdinalIgnoreCase));
}
