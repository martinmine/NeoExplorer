using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NavigationHistory _history = new(PathParser.ThisPC);

    public MainViewModel()
    {
        Folder = new FolderViewModel(Navigate);
        ThisPc = new ThisPcViewModel(Navigate);
        Network = new NetworkViewModel(Navigate);
        Sidebar = new SidebarViewModel(Navigate);
        _ = Sidebar.LoadAsync();
        Load(CurrentLocation);
    }

    public FolderViewModel Folder { get; }

    public ThisPcViewModel ThisPc { get; }

    public NetworkViewModel Network { get; }

    public SidebarViewModel Sidebar { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThisPC), nameof(IsNetwork), nameof(IsFolder))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand), nameof(GoForwardCommand), nameof(GoUpCommand))]
    public partial string CurrentLocation { get; private set; } = PathParser.ThisPC;

    public bool IsThisPC => CurrentLocation == PathParser.ThisPC;

    /// <summary>
    /// True for Network and for a network computer, which list computers and shared folders.
    /// The shared folders are ordinary folders.
    /// </summary>
    public bool IsNetwork => CurrentLocation == PathParser.Network || PathParser.IsNetworkComputer(CurrentLocation);

    public bool IsFolder => !IsThisPC && !IsNetwork;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderName), nameof(SearchPlaceholder))]
    public partial IReadOnlyList<PathSegment> Segments { get; private set; } = GetDisplaySegments(PathParser.ThisPC);

    public string FolderName => Segments[^1].Name;

    public string SearchPlaceholder => $"Search {FolderName}";

    public void Navigate(string location)
    {
        _history.Navigate(location);
        CurrentLocation = _history.Current;
    }

    /// <summary>
    /// Navigates to a path typed by the user. Returns false if it is not an existing folder.
    /// A network computer is always accepted; its page explains if it can't be reached.
    /// </summary>
    public bool TryNavigate(string input)
    {
        string? location = PathParser.Normalize(input);
        if (location is not null && location != PathParser.ThisPC && location != PathParser.Network && !PathParser.IsNetworkComputer(location))
        {
            location = FolderReader.FindFolder(location);
        }

        if (location is null)
        {
            return false;
        }

        Navigate(location);
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => CurrentLocation = _history.GoBack();

    private bool CanGoBack() => _history.CanGoBack;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward() => CurrentLocation = _history.GoForward();

    private bool CanGoForward() => _history.CanGoForward;

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void GoUp() => Navigate(PathParser.GetParent(CurrentLocation)!);

    private bool CanGoUp() => PathParser.GetParent(CurrentLocation) is not null;

    [RelayCommand]
    private void Refresh() => Load(CurrentLocation);

    partial void OnCurrentLocationChanged(string value)
    {
        Segments = GetDisplaySegments(value);
        Load(value);
    }

    private void Load(string location) =>
        _ = location == PathParser.ThisPC ? ThisPc.LoadAsync()
            : IsNetwork ? Network.LoadAsync(location)
            : Folder.LoadAsync(location);

    /// <summary>
    /// Shows drives the way File Explorer does, e.g. "Local Disk (C:)" instead of "C:",
    /// and starts with a computer icon segment, or a network icon segment for network locations.
    /// </summary>
    private static IReadOnlyList<PathSegment> GetDisplaySegments(string location) =>
        PathParser.GetSegments(location)
            .Select(s => s.Path.Length == 3 && s.Path.EndsWith(@":\", StringComparison.Ordinal) ? s with { Name = ShellInfo.GetDisplayName(s.Path) } : s)
            .Prepend(PathParser.IsNetworkLocation(location) ? IconSegment.Network : IconSegment.ThisPC)
            .ToList();
}

/// <summary>
/// The leading address bar segment, drawn as an icon like File Explorer.
/// </summary>
public sealed record IconSegment(string Name, string Path, string Glyph) : PathSegment(Name, Path)
{
    public static IconSegment ThisPC { get; } = new(PathParser.ThisPC, PathParser.ThisPC, "");

    public static IconSegment Network { get; } = new(PathParser.Network, PathParser.Network, "");
}
