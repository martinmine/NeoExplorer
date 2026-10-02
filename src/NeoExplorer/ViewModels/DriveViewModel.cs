using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

public partial class DriveViewModel(DriveItem drive) : ObservableObject
{
    public string Name => drive.Name;

    public string Path => drive.Path;

    public double UsedPercent => drive.TotalSize == 0 ? 0 : 100.0 * (drive.TotalSize - drive.FreeSpace) / drive.TotalSize;

    /// <summary>
    /// File Explorer shows the usage bar in red when a drive is over 90% full.
    /// </summary>
    public bool IsAlmostFull => UsedPercent >= 90;

    public string FreeSpace => $"{SizeFormatter.FormatCompact(drive.FreeSpace)} free of {SizeFormatter.FormatCompact(drive.TotalSize)}";

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    public async Task LoadIconAsync(uint size) => Icon = await ShellIcons.LoadAsync(Path, isFolder: true, size);

    // Used by UI Automation (screen readers) as the item's name.
    public override string ToString() => Name;
}
