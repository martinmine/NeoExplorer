using CommunityToolkit.Mvvm.ComponentModel;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

/// <summary>
/// The This PC page: the user's folders and the drives on this computer.
/// </summary>
public partial class ThisPcViewModel(Action<string> navigate) : ObservableObject
{
    private const uint IconSize = 48;

    // Physical pixels for the 48 px folder icons, sharp at up to 200% display scaling.
    private const int FolderIconPixels = 96;

    [ObservableProperty]
    public partial IReadOnlyList<KnownFolderViewModel> Folders { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<DriveViewModel> Drives { get; private set; } = [];

    public async Task LoadAsync()
    {
        IReadOnlyList<KnownFolderItem> folders = await Task.Run(KnownFolders.GetThisPcFolders);
        Folders = folders.Select(f => new KnownFolderViewModel(f)).ToList();
        foreach (KnownFolderViewModel folder in Folders)
        {
            folder.LoadIcon(FolderIconPixels);
        }

        IReadOnlyList<DriveItem> drives = await Task.Run(Services.Drives.GetDrives);
        Drives = drives.Select(d => new DriveViewModel(d)).ToList();
        foreach (DriveViewModel drive in Drives)
        {
            _ = drive.LoadIconAsync(IconSize);
        }
    }

    public void Open(KnownFolderViewModel folder) => navigate(folder.Path);

    public void Open(DriveViewModel drive) => navigate(drive.Path);
}
