using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

/// <summary>
/// The General and Details tabs of the Properties window for one file or folder.
/// </summary>
public partial class PropertiesViewModel : ObservableObject
{
    private readonly CancellationTokenSource _closing = new();
    private FileAttributes _attributes;

    public PropertiesViewModel(string path)
    {
        Path = path;
        IsFolder = Directory.Exists(path);
        Read();
    }

    public string Path { get; private set; }

    public bool IsFolder { get; }

    public bool IsFile => !IsFolder;

    /// <summary>
    /// The name as it is on disk; <see cref="EditedName"/> is what the name box shows.
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    public partial string EditedName { get; set; } = "";

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    /// <summary>
    /// "Text Document (.txt)" for a file, "File folder" for a folder.
    /// </summary>
    [ObservableProperty]
    public partial string Type { get; private set; } = "";

    [ObservableProperty]
    public partial string OpensWith { get; private set; } = "";

    [ObservableProperty]
    public partial ImageSource? OpensWithIcon { get; private set; }

    [ObservableProperty]
    public partial string Location { get; private set; } = "";

    [ObservableProperty]
    public partial string Size { get; private set; } = "";

    [ObservableProperty]
    public partial string SizeOnDisk { get; private set; } = "";

    /// <summary>
    /// "12 Files, 3 Folders", for folders.
    /// </summary>
    [ObservableProperty]
    public partial string Contains { get; private set; } = "";

    [ObservableProperty]
    public partial string Created { get; private set; } = "";

    [ObservableProperty]
    public partial string Modified { get; private set; } = "";

    [ObservableProperty]
    public partial string Accessed { get; private set; } = "";

    /// <summary>
    /// For a folder, null (the "■" state) until changed: Read-only on a folder applies to the files in it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    public partial bool? IsReadOnly { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    public partial bool? IsHidden { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<PropertyRow> Details { get; private set; } = [];

    /// <summary>
    /// Whether there is anything for Apply to do.
    /// </summary>
    public bool HasChanges => EditedName != Name || ReadOnlyChange is not null || HiddenChange is not null;

    /// <summary>
    /// The new Read-only state if it was changed, otherwise null.
    /// </summary>
    public bool? ReadOnlyChange => IsReadOnly is bool value && (IsFolder || value != _attributes.HasFlag(FileAttributes.ReadOnly)) ? value : null;

    public bool? HiddenChange => IsHidden is bool value && value != _attributes.HasFlag(FileAttributes.Hidden) ? value : null;

    /// <summary>
    /// Loads what takes a while: icons, the folder size, and the Details tab. Must be called on the UI thread.
    /// </summary>
    public async Task LoadAsync()
    {
        Icon = await ShellIcons.LoadAsync(Path, IsFolder, 32);
        if (IsFolder)
        {
            await MeasureFolderAsync();
        }
        else
        {
            await LoadOpensWithAsync();
            Details = await ShellProperties.GetDetailsAsync(Path);
        }
    }

    /// <summary>
    /// Shows the app chosen in Windows' "How do you want to open this file?" dialog after Change...
    /// </summary>
    public async Task LoadOpensWithAsync()
    {
        AppInfo? app = ShellAssociations.GetDefaultApp(System.IO.Path.GetExtension(Path));
        OpensWith = app?.Name ?? "Pick an app";
        OpensWithIcon = await ShellAssociations.LoadIconAsync(app?.IconReference, 16);
    }

    /// <summary>
    /// Applies the new name and attributes. Returns false if something couldn't be changed; Windows has shown why.
    /// </summary>
    public async Task<bool> ApplyAsync(bool attributesIncludeContents, nint owner)
    {
        bool ok = true;
        FileAttributes set = 0;
        FileAttributes clear = 0;
        if (ReadOnlyChange is bool readOnly)
        {
            if (readOnly)
            {
                set |= FileAttributes.ReadOnly;
            }
            else
            {
                clear |= FileAttributes.ReadOnly;
            }
        }

        if (HiddenChange is bool hidden)
        {
            if (hidden)
            {
                set |= FileAttributes.Hidden;
            }
            else
            {
                clear |= FileAttributes.Hidden;
            }
        }

        if (set != 0 || clear != 0)
        {
            string path = Path;
            bool includeContents = IsFolder && attributesIncludeContents;
            ok = await Task.Run(() => AttributeChanger.Apply(path, set, clear, includeContents)) == 0;
        }

        if (EditedName != Name)
        {
            if (await FileOperations.RenameAsync(Path, EditedName, owner) is string newPath)
            {
                Path = newPath;
            }
            else
            {
                ok = false;
            }
        }

        Read();
        return ok;
    }

    public void Cancel() => _closing.Cancel();

    /// <summary>
    /// Reads the name, dates and attributes, which are quick.
    /// </summary>
    private void Read()
    {
        FileSystemInfo info = IsFolder ? new DirectoryInfo(Path) : new FileInfo(Path);
        Name = info.Name;
        EditedName = Name;
        Location = System.IO.Path.GetDirectoryName(Path) ?? "";
        Type = IsFolder ? ShellInfo.GetTypeName(info) : PropertiesFormatter.TypeOfFile(ShellInfo.GetTypeName(info), info.Extension);
        Created = PropertiesFormatter.Date(info.CreationTime);
        Modified = PropertiesFormatter.Date(info.LastWriteTime);
        Accessed = PropertiesFormatter.Date(info.LastAccessTime);
        _attributes = info.Attributes;
        IsReadOnly = IsFolder ? null : _attributes.HasFlag(FileAttributes.ReadOnly);
        IsHidden = _attributes.HasFlag(FileAttributes.Hidden);
        if (info is FileInfo file)
        {
            Size = SizeFormatter.FormatWithBytes(file.Length);
            long onDisk = ShellProperties.GetAllocationSize(Path) is long allocation
                ? SizeFormatter.SizeOnDisk(allocation, ShellProperties.GetClusterSize(Path))
                : SizeFormatter.RoundUpToCluster(file.Length, ShellProperties.GetClusterSize(Path));
            SizeOnDisk = SizeFormatter.FormatWithBytes(onDisk);
        }

        OnPropertyChanged(nameof(HasChanges));
    }

    /// <summary>
    /// Counts up the size and contents while a large folder is measured, as File Explorer does.
    /// </summary>
    private async Task MeasureFolderAsync()
    {
        var progress = new Progress<FolderSizeInfo>(ShowFolderSize);
        string path = Path;
        long clusterSize = ShellProperties.GetClusterSize(path);
        try
        {
            ShowFolderSize(await Task.Run(() => FolderSize.Measure(path, clusterSize, progress, _closing.Token)));
        }
        catch (OperationCanceledException)
        {
            // The window was closed.
        }
    }

    private void ShowFolderSize(FolderSizeInfo info)
    {
        Size = SizeFormatter.FormatWithBytes(info.Size);
        SizeOnDisk = SizeFormatter.FormatWithBytes(info.SizeOnDisk);
        Contains = PropertiesFormatter.Contains(info.Files, info.Folders);
    }
}
