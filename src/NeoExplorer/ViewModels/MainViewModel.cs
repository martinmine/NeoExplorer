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
        Sidebar = new SidebarViewModel(Navigate);
        _ = Sidebar.LoadAsync();
        Load(CurrentLocation);
    }

    public FolderViewModel Folder { get; }

    public ThisPcViewModel ThisPc { get; }

    public SidebarViewModel Sidebar { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThisPC), nameof(IsFolder))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand), nameof(GoForwardCommand), nameof(GoUpCommand))]
    public partial string CurrentLocation { get; private set; } = PathParser.ThisPC;

    public bool IsThisPC => CurrentLocation == PathParser.ThisPC;

    public bool IsFolder => !IsThisPC;

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
    /// </summary>
    public bool TryNavigate(string input)
    {
        string? location = PathParser.Normalize(input);
        if (location is not null && location != PathParser.ThisPC)
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

    private void Load(string location) => _ = location == PathParser.ThisPC ? ThisPc.LoadAsync() : Folder.LoadAsync(location);

    /// <summary>
    /// Shows drives the way File Explorer does, e.g. "Local Disk (C:)" instead of "C:",
    /// and starts with the computer icon segment.
    /// </summary>
    private static IReadOnlyList<PathSegment> GetDisplaySegments(string location) =>
        PathParser.GetSegments(location)
            .Select(s => s.Path.Length == 3 && s.Path.EndsWith(@":\", StringComparison.Ordinal) ? s with { Name = ShellInfo.GetDisplayName(s.Path) } : s)
            .Prepend(new ComputerSegment())
            .ToList();
}

/// <summary>
/// The leading address bar segment, drawn as a computer icon like File Explorer.
/// </summary>
public sealed record ComputerSegment() : PathSegment(PathParser.ThisPC, PathParser.ThisPC);
