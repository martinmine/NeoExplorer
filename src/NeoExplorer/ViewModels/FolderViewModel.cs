using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

/// <summary>
/// The contents of the current folder: loading, filtering, searching, sorting and opening items.
/// </summary>
public partial class FolderViewModel(Action<string> navigate) : ObservableObject
{
    private const string UpGlyph = "";
    private const string DownGlyph = "";
    private const int SearchBatchMilliseconds = 200;

    private IReadOnlyList<ItemViewModel> _folderItems = [];
    private ObservableCollection<ItemViewModel> _searchResults = [];
    private CancellationTokenSource? _loading;
    private string _location = "";
    private string _filter = "";
    private string _query = "";
    private string _error = "";

    [ObservableProperty]
    public partial IReadOnlyList<ItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Shown instead of the items, e.g. "This folder is empty." or an access error.
    /// </summary>
    [ObservableProperty]
    public partial string Message { get; private set; } = "";

    /// <summary>
    /// True while showing results from searching subfolders, rather than the folder itself.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSearchResults { get; private set; }

    [ObservableProperty]
    public partial bool IsSearching { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameSortGlyph), nameof(DateModifiedSortGlyph), nameof(TypeSortGlyph), nameof(SizeSortGlyph))]
    public partial SortColumn SortColumn { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameSortGlyph), nameof(DateModifiedSortGlyph), nameof(TypeSortGlyph), nameof(SizeSortGlyph))]
    public partial bool SortDescending { get; private set; }

    public string NameSortGlyph => SortGlyph(SortColumn.Name);

    public string DateModifiedSortGlyph => SortGlyph(SortColumn.DateModified);

    public string TypeSortGlyph => SortGlyph(SortColumn.Type);

    public string SizeSortGlyph => SortGlyph(SortColumn.Size);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailsView), nameof(IsIconView), nameof(IconSize))]
    public partial ViewMode ViewMode { get; set; }

    public bool IsDetailsView => ViewMode == ViewMode.Details;

    public bool IsIconView => !IsDetailsView;

    /// <summary>
    /// Icon size in DPI-independent pixels for the current view.
    /// </summary>
    public uint IconSize => ViewMode switch
    {
        ViewMode.MediumIcons => 48,
        ViewMode.LargeIcons => 96,
        ViewMode.ExtraLargeIcons => 256,
        _ => 16,
    };

    public void Zoom(int steps) => ViewMode = ViewModes.Zoom(ViewMode, steps);

    /// <summary>
    /// Shows a new location, clearing any filter or search.
    /// </summary>
    public Task LoadAsync(string location)
    {
        _location = location;
        _filter = "";
        return ReloadAsync();
    }

    /// <summary>
    /// Reloads the folder, or runs the search again when showing search results.
    /// </summary>
    public Task RefreshAsync() => IsSearchResults ? SearchAsync(_query) : ReloadAsync();

    /// <summary>
    /// Shows only the items in the current folder whose names match <paramref name="text"/>.
    /// Stops a running search and leaves the search results.
    /// </summary>
    public void Filter(string text)
    {
        if (IsSearchResults)
        {
            _loading?.Cancel();
            IsSearchResults = false;
            IsSearching = false;
        }

        _filter = text.Trim();
        ApplySort();
    }

    /// <summary>
    /// Searches the current folder and its subfolders. Results appear while the search runs.
    /// Starting a new search, filtering or loading a location cancels it.
    /// </summary>
    public async Task SearchAsync(string query)
    {
        query = query.Trim();
        if (query == "")
        {
            Filter("");
            return;
        }

        CancellationTokenSource searching = StartLoading();
        ObservableCollection<ItemViewModel> results = _searchResults = [];
        _query = query;
        _error = "";
        IsSearchResults = true;
        IsSearching = true;
        Items = results;
        UpdateMessage();

        // Progress<T> runs the callback on the UI thread.
        var progress = new Progress<List<FileSystemItem>>(batch =>
        {
            if (!searching.IsCancellationRequested)
            {
                foreach (FileSystemItem item in batch)
                {
                    results.Add(new ItemViewModel(item, isSearchResult: true));
                }
            }
        });

        string location = _location;
        try
        {
            await Task.Run(() => Search(location, query, progress, searching.Token), searching.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            _error = e.Message;
        }

        if (searching.IsCancellationRequested)
        {
            return;
        }

        IsSearching = false;

        // If the user sorted during the search, the list shown is a sorted copy without the latest results.
        if (!ReferenceEquals(Items, results))
        {
            ApplySort();
        }

        UpdateMessage();
    }

    /// <summary>
    /// Sorts by a column. Choosing the current column again reverses the order.
    /// </summary>
    public void SortBy(SortColumn column) => Sort(column, column == SortColumn && !SortDescending);

    public void Sort(SortColumn column, bool descending)
    {
        SortColumn = column;
        SortDescending = descending;
        ApplySort();
    }

    /// <summary>
    /// Opens a folder in NeoExplorer, or a file in its default app.
    /// </summary>
    public void Open(ItemViewModel item)
    {
        if (item.Item.IsFolder)
        {
            navigate(item.Item.Path);
            return;
        }

        try
        {
            // ErrorDialog lets Windows show its own "Open with" or error dialog.
            Process.Start(new ProcessStartInfo(item.Item.Path) { UseShellExecute = true, ErrorDialog = true });
        }
        catch (Win32Exception)
        {
            // Windows has already told the user, or they cancelled the "Open with" dialog.
        }
    }

    private async Task ReloadAsync()
    {
        CancellationTokenSource loading = StartLoading();
        IsSearchResults = false;
        IsSearching = false;

        string location = _location;
        IReadOnlyList<FileSystemItem> items;
        string error = "";
        try
        {
            items = await Task.Run(() => FolderReader.Read(location, ShellInfo.GetTypeName, loading.Token), loading.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            items = [];
            error = e.Message;
        }

        if (loading.IsCancellationRequested)
        {
            return;
        }

        _folderItems = items.Select(i => new ItemViewModel(i)).ToList();
        _error = error;
        ApplySort();
    }

    /// <summary>
    /// Cancels the running load or search and returns a token for the next one.
    /// </summary>
    private CancellationTokenSource StartLoading()
    {
        _loading?.Cancel();
        return _loading = new CancellationTokenSource();
    }

    /// <summary>
    /// Runs on a background thread and hands results to the UI in batches, so it isn't flooded.
    /// </summary>
    private static void Search(string location, string query, IProgress<List<FileSystemItem>> progress, CancellationToken cancellationToken)
    {
        var batch = new List<FileSystemItem>();
        var sinceReport = Stopwatch.StartNew();
        foreach (FileSystemItem item in FolderReader.Search(location, query, ShellInfo.GetTypeName, cancellationToken))
        {
            batch.Add(item);
            if (sinceReport.ElapsedMilliseconds >= SearchBatchMilliseconds)
            {
                progress.Report(batch);
                batch = [];
                sinceReport.Restart();
            }
        }

        progress.Report(batch);
    }

    private void ApplySort()
    {
        IEnumerable<ItemViewModel> items = IsSearchResults
            ? _searchResults
            : _folderItems.Where(i => NameFilter.Matches(i.Name, _filter));
        Items = items.OrderBy(i => i.Item, new ItemComparer(SortColumn, SortDescending)).ToList();
        UpdateMessage();
    }

    private void UpdateMessage()
    {
        Message = _error != "" ? _error
            : IsSearching || Items.Count > 0 ? ""
            : IsSearchResults || _filter != "" ? "No items match your search."
            : "This folder is empty.";
    }

    private string SortGlyph(SortColumn column) => column != SortColumn ? "" : SortDescending ? DownGlyph : UpGlyph;
}
