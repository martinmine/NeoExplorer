using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

public partial class KnownFolderViewModel(KnownFolderItem folder) : ObservableObject
{
    public string Name => folder.Name;

    public string Path => folder.Path;

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    /// <summary>
    /// The size is in physical pixels. Must be called on the UI thread.
    /// </summary>
    public void LoadIcon(int size) => Icon = ShellIcons.LoadShellLocation(Path, size);

    // Used by UI Automation (screen readers) as the item's name.
    public override string ToString() => Name;
}
