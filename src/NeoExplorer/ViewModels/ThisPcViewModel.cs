using CommunityToolkit.Mvvm.ComponentModel;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

/// <summary>
/// The This PC page: the drives on this computer.
/// </summary>
public partial class ThisPcViewModel(Action<string> navigate) : ObservableObject
{
    private const uint IconSize = 48;

    [ObservableProperty]
    public partial IReadOnlyList<DriveViewModel> Drives { get; private set; } = [];

    public async Task LoadAsync()
    {
        IReadOnlyList<DriveItem> drives = await Task.Run(Services.Drives.GetDrives);
        Drives = drives.Select(d => new DriveViewModel(d)).ToList();
        foreach (DriveViewModel drive in Drives)
        {
            _ = drive.LoadIconAsync(IconSize);
        }
    }

    public void Open(DriveViewModel drive) => navigate(drive.Path);
}
