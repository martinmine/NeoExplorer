using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

/// <summary>
/// The contents of the current folder: loading, sorting and opening items.
/// </summary>
public partial class FolderViewModel(Action<string> navigate) : ObservableObject
{
    private const string UpGlyph = "\uE70E";
    private const string DownGlyph = "\uE70D";

    private IReadOnlyList<ItemViewModel> _allItems = [];
    private CancellationTokenSource? _loading;
    private string _location = PathParser.ThisPC;

    [ObservableProperty]
    public partial IReadOnlyList<ItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Shown instead of the items, e.g. "This folder is empty." or an access error.
    /// </summary>
    [ObservableProperty]
    public partial string Message { get; private set; } = "";

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
    /// Loads a location on a background thread. Starting a new load cancels the previous one.
    /// </summary>
    public async Task LoadAsync(string location)
    {
        _location = location;
        _loading?.Cancel();
        var loading = _loading = new CancellationTokenSource();

        IReadOnlyList<FileSystemItem> items;
        string message = "";
        try
        {
            items = await Task.Run(() => Read(location, loading.Token), loading.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            items = [];
            message = e.Message;
        }

        if (loading.IsCancellationRequested)
        {
            return;
        }

        _allItems = items.Select(i => new ItemViewModel(i)).ToList();
        Message = items.Count == 0 && message == "" ? "This folder is empty." : message;
        ApplySort();
    }

    public Task RefreshAsync() => LoadAsync(_location);

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

    private static IReadOnlyList<FileSystemItem> Read(string location, CancellationToken cancellationToken)
    {
        if (location != PathParser.ThisPC)
        {
            return FolderReader.Read(location, FileTypes.GetTypeName, cancellationToken);
        }

        // Temporary list of drives until the This PC view is built (phase 6).
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new FileSystemItem(d.Name.TrimEnd('\\'), d.Name, true, d.RootDirectory.LastWriteTime, null, FileTypes.GetTypeName(d.RootDirectory)))
            .ToList();
    }

    private void ApplySort()
    {
        Items = _allItems.OrderBy(i => i.Item, new ItemComparer(SortColumn, SortDescending)).ToList();
    }

    private string SortGlyph(SortColumn column) => column != SortColumn ? "" : SortDescending ? DownGlyph : UpGlyph;
}
