using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoExplorer.Core;

namespace NeoExplorer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NavigationHistory _history = new(PathParser.ThisPC);

    public MainViewModel()
    {
        Folder = new FolderViewModel(Navigate);
        _ = Folder.LoadAsync(CurrentLocation);
    }

    public FolderViewModel Folder { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Segments), nameof(FolderName), nameof(SearchPlaceholder))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand), nameof(GoForwardCommand), nameof(GoUpCommand))]
    public partial string CurrentLocation { get; private set; } = PathParser.ThisPC;

    public IReadOnlyList<PathSegment> Segments => PathParser.GetSegments(CurrentLocation);

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
    private void Refresh() => _ = Folder.LoadAsync(CurrentLocation);

    partial void OnCurrentLocationChanged(string value) => _ = Folder.LoadAsync(value);
}
