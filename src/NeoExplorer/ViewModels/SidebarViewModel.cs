using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

public partial class SidebarItem(string name, string path, bool isPinned = false, string glyph = "") : ObservableObject
{
    private const string ThisPCParsingName = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    private ImageSource? _icon;
    private bool _isIconRequested;

    public string Name => name;

    /// <summary>
    /// A folder path, This PC, Network, a network computer ("\\SERVER"), or a shell path such as
    /// "::{645FF040-...}" for the Recycle Bin.
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

    /// <summary>
    /// True for drives and the folders under them, which list their subfolders when expanded. Also for Network,
    /// which lists computers, and for computers, which list their shared folders.
    /// </summary>
    public bool ShowsSubfolders { get; private init; }

    public ObservableCollection<SidebarItem> Children { get; } = [];

    /// <summary>
    /// Shows the expand arrow before the subfolders are loaded.
    /// </summary>
    [ObservableProperty]
    public partial bool HasUnrealizedChildren { get; set; }

    /// <summary>
    /// Loaded the first time the tree shows the item, so expanding a folder with thousands of subfolders
    /// doesn't load thousands of icons.
    /// </summary>
    public ImageSource? Icon
    {
        get
        {
            if (!_isIconRequested)
            {
                _isIconRequested = true;
                _ = LoadIconAsync();
            }

            return _icon;
        }
        private set => SetProperty(ref _icon, value);
    }

    public static SidebarItem Folder(string name, string path, bool hasSubfolders) =>
        new(name, path) { ShowsSubfolders = true, HasUnrealizedChildren = hasSubfolders };

    /// <summary>
    /// Lists the subfolders. Called each time the item is expanded, so the tree picks up folders created
    /// or deleted since. Items that are still there are kept, so folders expanded below stay expanded.
    /// </summary>
    public async Task LoadChildrenAsync()
    {
        IReadOnlyList<SubfolderItem> folders;
        try
        {
            folders = await ReadChildrenAsync();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception)
        {
            folders = [];
        }

        var existing = Children.ToDictionary(c => c.Path, StringComparer.OrdinalIgnoreCase);
        var updated = new List<SidebarItem>(folders.Count);
        foreach (SubfolderItem folder in folders)
        {
            if (existing.TryGetValue(folder.Path, out SidebarItem? item))
            {
                // A folder that was never expanded may have gained or lost subfolders.
                if (item.Children.Count == 0)
                {
                    item.HasUnrealizedChildren = folder.HasSubfolders;
                }
            }
            else
            {
                item = Folder(folder.Name, folder.Path, folder.HasSubfolders);
            }

            updated.Add(item);
        }

        var kept = updated.ToHashSet();
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (!kept.Contains(Children[i]))
            {
                Children.RemoveAt(i);
            }
        }

        // Now Children is in order and a subset of updated, so this only inserts the new folders.
        for (int i = 0; i < updated.Count; i++)
        {
            if (i == Children.Count || Children[i] != updated[i])
            {
                Children.Insert(i, updated[i]);
            }
        }

        HasUnrealizedChildren = false;
    }

    private async Task<IReadOnlyList<SubfolderItem>> ReadChildrenAsync()
    {
        // Computers and shares get an expand arrow without asking them first, which would be slow over the network.
        if (path == PathParser.Network)
        {
            // Show computers as they're found instead of after discovery finishes. Reports arrive on the UI thread.
            var found = new Progress<NetworkItem>(computer =>
            {
                if (!Children.Any(c => string.Equals(c.Path, computer.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    int index = 0;
                    while (index < Children.Count && ItemComparer.CompareNames(Children[index].Name, computer.Name) < 0)
                    {
                        index++;
                    }

                    Children.Insert(index, Folder(computer.Name, computer.Path, hasSubfolders: true));
                }
            });
            IReadOnlyList<NetworkItem> computers = await Services.Network.GetComputersAsync(found);
            return computers.Select(c => new SubfolderItem(c.Name, c.Path, HasSubfolders: true)).ToList();
        }

        if (PathParser.IsNetworkComputer(path))
        {
            IReadOnlyList<NetworkItem> shares = await Task.Run(() => Services.Network.GetShares(path));
            return shares.Select(s => new SubfolderItem(s.Name, s.Path, HasSubfolders: true)).ToList();
        }

        return await Task.Run(() => FolderReader.ReadSubfolders(path));
    }

    public IEnumerable<SidebarItem> SelfAndDescendants() => Children.SelectMany(c => c.SelfAndDescendants()).Prepend(this);

    private async Task LoadIconAsync()
    {
        // Don't change Icon while the binding is still reading it.
        await Task.Yield();

        // 32 px stays sharp at up to 200% display scaling.
        if (path == PathParser.ThisPC)
        {
            Icon = ShellIcons.LoadShellLocation(ThisPCParsingName, 32);
        }
        else if (path == PathParser.Network)
        {
            Icon = ShellIcons.LoadShellLocation(Services.Network.ShellParsingName, 32);
        }
        else if (PathParser.IsNetworkLocation(path))
        {
            // Computers and shares have their own icons, and this doesn't block the UI on a slow network.
            Icon = await ShellIcons.LoadShellLocationAsync(path, 32);
        }
        else if (!IsFileSystem && glyph == "")
        {
            Icon = ShellIcons.LoadShellLocation(path, 32);
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
/// The navigation pane: Quick Access folders, then This PC with its drives and their folders, then Network.
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
    /// Expands to the computers on the network, then their shared folders and the folders in those.
    /// </summary>
    public SidebarItem Network { get; } = SidebarItem.Folder(PathParser.Network, PathParser.Network, hasSubfolders: true);

    /// <summary>
    /// The items of the tree below Quick Access.
    /// </summary>
    public IReadOnlyList<SidebarItem> DeviceItems => [ThisPC, Network];

    public async Task LoadAsync()
    {
        QuickAccessItems = QuickAccess.GetFolders()
            .Select(f => new SidebarItem(f.Name, f.Path, f.IsPinned, f.IsFileSystem ? "" : f.Path == RecycleBinPath ? RecycleBinGlyph : ShellFolderGlyph))
            .ToList();

        IReadOnlyList<DriveItem> drives = await Task.Run(Drives.GetDrives);
        foreach (DriveItem drive in drives)
        {
            // Checking every drive for folders could wait on slow network drives; nearly all drives have some.
            ThisPC.Children.Add(SidebarItem.Folder(drive.Name, drive.Path, hasSubfolders: true));
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
    /// Quick Access comes first; folders under drives are only found once their parent has been expanded.
    /// </summary>
    public SidebarItem? Find(string location) =>
        QuickAccessItems.Concat(DeviceItems.SelectMany(i => i.SelfAndDescendants()))
            .FirstOrDefault(i => string.Equals(i.Path, location, StringComparison.OrdinalIgnoreCase));
}
