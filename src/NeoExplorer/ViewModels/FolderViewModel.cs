using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

/// <summary>
/// The contents of the current folder: loading, filtering, searching, sorting, opening items, and file operations.
/// </summary>
public partial class FolderViewModel : ObservableObject
{
    private const string UpGlyph = "";
    private const string DownGlyph = "";
    private const int SearchBatchMilliseconds = 200;

    // HRESULT for ERROR_NOT_READY, e.g. an empty DVD drive.
    private const int ErrorNotReady = unchecked((int)0x80070015);

    // How long to wait after the folder changes on disk before showing the changes, so a burst
    // of changes (e.g. copying many files) refreshes the view a few times rather than for every file.
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(300);

    private readonly Action<string> _navigate;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _refreshTimer;
    private IReadOnlyList<ItemViewModel> _folderItems = [];
    private ObservableCollection<ItemViewModel> _searchResults = [];
    private CancellationTokenSource? _loading;
    private FileSystemWatcher? _watcher;
    private HashSet<string>? _pathsToSelect;
    private string _location = "";
    private string _filter = "";
    private string _query = "";
    private string _error = "";

    public FolderViewModel(Action<string> navigate)
    {
        _navigate = navigate;
        _refreshTimer = _dispatcher.CreateTimer();
        _refreshTimer.Interval = RefreshDelay;
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Tick += (_, _) => RefreshAfterChange();
        ShellClipboard.CutChanged += (_, _) => UpdateCut();
    }

    /// <summary>
    /// Raised after <see cref="Items"/> is replaced, with the items that should be selected: the ones that were
    /// selected before, or the ones an operation such as paste or rename just created.
    /// </summary>
    public event EventHandler<SelectionRequest>? SelectionRestoring;

    [ObservableProperty]
    public partial IReadOnlyList<ItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// The selected items, kept up to date by the view.
    /// </summary>
    public IReadOnlyList<ItemViewModel> SelectedItems { get; set; } = [];

    /// <summary>
    /// The folder shown, or searched in.
    /// </summary>
    public string Location => _location;

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

    /// <summary>
    /// The view used for all folders. It is remembered between runs.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailsView), nameof(IsIconView), nameof(IconSize))]
    public partial ViewMode ViewMode { get; set; } = Settings.ViewMode;

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

    partial void OnViewModeChanged(ViewMode value) => Settings.ViewMode = value;

    /// <summary>
    /// Shows a new location, clearing any filter or search.
    /// </summary>
    public Task LoadAsync(string location)
    {
        if (location != _location)
        {
            _location = location;
            Watch(location);
        }

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
            _error = Describe(e);
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
            _navigate(item.Item.Path);
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

    /// <summary>
    /// Opens several items: every file in its app, and the first folder in NeoExplorer.
    /// </summary>
    public void Open(IReadOnlyList<ItemViewModel> items)
    {
        foreach (ItemViewModel file in items.Where(i => !i.Item.IsFolder))
        {
            Open(file);
        }

        if (items.FirstOrDefault(i => i.Item.IsFolder) is ItemViewModel folder)
        {
            Open(folder);
        }
    }

    /// <summary>
    /// Whether items can be pasted or dropped into the view itself. Search results come from many folders.
    /// </summary>
    public bool CanPasteHere => !IsSearchResults && _error == "";

    public Task CopyAsync(IReadOnlyList<ItemViewModel> items) => ShellClipboard.SetAsync(Paths(items), cut: false);

    public Task CutAsync(IReadOnlyList<ItemViewModel> items) => ShellClipboard.SetAsync(Paths(items), cut: true);

    /// <summary>
    /// Pastes files from the clipboard into <paramref name="folder"/>, or into the folder shown.
    /// </summary>
    public async Task PasteAsync(string? folder = null)
    {
        folder ??= _location;
        if (await ShellClipboard.GetAsync() is not (IReadOnlyList<string> paths, bool cut))
        {
            return;
        }

        if (cut)
        {
            if (DropEffects.CanMove(paths, folder))
            {
                IReadOnlyList<string> moved = await FileOperations.MoveAsync(paths, folder, App.WindowHandle);
                ShellClipboard.ClearAfterMove();
                await ShowResultAsync(folder, moved);
            }
        }
        else if (DropEffects.CanCopy(paths, folder))
        {
            await ShowResultAsync(folder, await FileOperations.CopyAsync(paths, folder, App.WindowHandle));
        }
    }

    /// <summary>
    /// Copies or moves dropped files into <paramref name="folder"/>.
    /// </summary>
    public async Task DropAsync(IReadOnlyList<string> paths, string folder, DropOperation operation)
    {
        IReadOnlyList<string> results = operation switch
        {
            DropOperation.Move => await FileOperations.MoveAsync(paths, folder, App.WindowHandle),
            DropOperation.Copy => await FileOperations.CopyAsync(paths, folder, App.WindowHandle),
            _ => [],
        };
        await ShowResultAsync(folder, results);
    }

    /// <summary>
    /// Sends items to the Recycle Bin, or deletes them for good.
    /// </summary>
    public async Task DeleteAsync(IReadOnlyList<ItemViewModel> items, bool permanently)
    {
        // Afterwards the item after the deleted ones is selected (or the one before, at the end), as in File Explorer.
        var deleted = items.ToHashSet();
        int first = Items.ToList().FindIndex(deleted.Contains);
        ItemViewModel? next = Items.Skip(first + 1).FirstOrDefault(i => !deleted.Contains(i))
            ?? Items.Take(Math.Max(first, 0)).LastOrDefault(i => !deleted.Contains(i));

        await FileOperations.DeleteAsync(Paths(items), permanently, App.WindowHandle);

        // If the delete was cancelled, the items stay selected instead.
        bool deletedAll = !items.Any(i => Path.Exists(i.Item.Path));
        await ShowResultAsync(_location, deletedAll && next is not null ? [next.Item.Path] : []);
    }

    /// <summary>
    /// Renames an item to a name that has already been checked, and selects it afterwards.
    /// </summary>
    public async Task RenameAsync(ItemViewModel item, string newName)
    {
        string? newPath = await FileOperations.RenameAsync(item.Item.Path, newName, App.WindowHandle);
        await ShowResultAsync(Path.GetDirectoryName(item.Item.Path), newPath is null ? [] : [newPath]);
    }

    private static List<string> Paths(IReadOnlyList<ItemViewModel> items) => items.Select(i => i.Item.Path).ToList();

    /// <summary>
    /// Shows the folder after an operation, selecting the items it created when they are in the folder shown.
    /// Search results are searched again, since items may have been renamed or deleted.
    /// </summary>
    private Task ShowResultAsync(string? folder, IReadOnlyList<string> newPaths)
    {
        if (IsSearchResults)
        {
            return SearchAsync(_query);
        }

        if (newPaths.Count > 0 && DropEffects.IsSamePath(folder, _location))
        {
            _pathsToSelect = new(newPaths, StringComparer.OrdinalIgnoreCase);
        }

        return ReloadAsync();
    }

    /// <summary>
    /// Watches the folder shown, so changes made by NeoExplorer, by other apps or by Windows appear by themselves.
    /// </summary>
    private void Watch(string location)
    {
        _watcher?.Dispose();
        _watcher = null;
        try
        {
            var watcher = new FileSystemWatcher(location)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size
                    | NotifyFilters.LastWrite | NotifyFilters.Attributes,
            };
            watcher.Created += Folder_Changed;
            watcher.Deleted += Folder_Changed;
            watcher.Changed += Folder_Changed;
            watcher.Renamed += Folder_Changed;
            watcher.Error += Folder_Changed;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // The folder can't be watched, e.g. some network drives; F5 still refreshes it.
        }
    }

    // Raised on a background thread.
    private void Folder_Changed(object sender, EventArgs e) => _dispatcher.TryEnqueue(() =>
    {
        // Refreshes at most once per delay, even while a long copy keeps changing the folder.
        if (!_refreshTimer.IsRunning)
        {
            _refreshTimer.Start();
        }
    });

    private void RefreshAfterChange()
    {
        // Search results stay as they are, like in File Explorer. Wait with the refresh while a name is
        // being edited, since replacing the items would end the editing.
        if (IsSearchResults)
        {
            return;
        }

        if (_folderItems.Any(i => i.IsRenaming))
        {
            _refreshTimer.Start();
            return;
        }

        _ = ReloadAsync();
    }

    private void UpdateCut()
    {
        foreach (ItemViewModel item in _folderItems.Concat(_searchResults))
        {
            item.IsCut = ShellClipboard.IsCut(item.Item.Path);
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
            error = Describe(e);
        }

        if (loading.IsCancellationRequested)
        {
            return;
        }

        // Keep the view models of unchanged items, so their icons don't load again and blink.
        var previous = new Dictionary<string, ItemViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (ItemViewModel item in _folderItems)
        {
            // Case-sensitive folders (e.g. from WSL) can have names that differ only in case.
            previous.TryAdd(item.Item.Path, item);
        }

        _folderItems = items
            .Select(i => previous.TryGetValue(i.Path, out ItemViewModel? old) && old.Item == i ? old : new ItemViewModel(i))
            .ToList();
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
        // Keep the selection, by path since reloading creates new items for changed files.
        bool reveal = _pathsToSelect is not null;
        HashSet<string> select = _pathsToSelect ?? new(SelectedItems.Select(i => i.Item.Path), StringComparer.OrdinalIgnoreCase);
        _pathsToSelect = null;

        IEnumerable<ItemViewModel> items = IsSearchResults
            ? _searchResults
            : _folderItems.Where(i => NameFilter.Matches(i.Name, _filter));
        Items = items.OrderBy(i => i.Item, new ItemComparer(SortColumn, SortDescending)).ToList();
        UpdateMessage();

        List<ItemViewModel> selected = Items.Where(i => select.Contains(i.Item.Path)).ToList();
        if (selected.Count > 0)
        {
            SelectionRestoring?.Invoke(this, new SelectionRequest(selected, reveal));
        }
    }

    /// <summary>
    /// A message for the user instead of the technical exception text, which includes paths and jargon.
    /// </summary>
    private static string Describe(Exception e) => e switch
    {
        UnauthorizedAccessException => "You don't have permission to open this folder.",
        DirectoryNotFoundException => "This folder doesn't exist anymore. It may have been moved or deleted.",
        DriveNotFoundException => "This drive isn't available. It may have been disconnected.",
        IOException when e.HResult == ErrorNotReady => "This drive isn't ready. Insert a disc or connect the drive, then refresh.",
        _ => e.Message,
    };

    private void UpdateMessage()
    {
        Message = _error != "" ? _error
            : IsSearching || Items.Count > 0 ? ""
            : IsSearchResults || _filter != "" ? "No items match your search."
            : "This folder is empty.";
    }

    private string SortGlyph(SortColumn column) => column != SortColumn ? "" : SortDescending ? DownGlyph : UpGlyph;
}

/// <summary>
/// Items for the view to select. <see cref="Reveal"/> is true for items an operation just created,
/// which are scrolled into view; a refresh keeps the scroll position instead.
/// </summary>
public sealed record SelectionRequest(IReadOnlyList<ItemViewModel> Items, bool Reveal);
