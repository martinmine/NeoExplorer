using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public sealed class FolderReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("NeoExplorerTests").FullName;

    private static string TypeName(FileSystemInfo info) => info is DirectoryInfo ? "File folder" : info.Extension;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Read_ReturnsFilesAndFolders()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "Photos")).FullName;
        string file = Path.Combine(_root, "notes.txt");
        File.WriteAllText(file, "hello");
        var modified = new DateTime(2026, 5, 22, 8, 25, 0);
        File.SetLastWriteTime(file, modified);

        var items = FolderReader.Read(_root, TypeName).OrderBy(i => i.Name).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(new FileSystemItem("notes.txt", file, false, modified, 5, ".txt"), items[0]);
        Assert.Equal("Photos", items[1].Name);
        Assert.Equal(folder, items[1].Path);
        Assert.True(items[1].IsFolder);
        Assert.Null(items[1].Size);
        Assert.Equal("File folder", items[1].Type);
    }

    [Fact]
    public void Read_SkipsHiddenAndSystemItems()
    {
        string hidden = Path.Combine(_root, "hidden.txt");
        string system = Path.Combine(_root, "system.txt");
        File.WriteAllText(hidden, "");
        File.WriteAllText(system, "");
        File.WriteAllText(Path.Combine(_root, "visible.txt"), "");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        File.SetAttributes(system, FileAttributes.System);

        var items = FolderReader.Read(_root, TypeName);

        Assert.Equal("visible.txt", Assert.Single(items).Name);
    }

    [Fact]
    public void Read_EmptyFolder_ReturnsEmptyList()
    {
        Assert.Empty(FolderReader.Read(_root, TypeName));
    }

    [Fact]
    public void Read_MissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => FolderReader.Read(Path.Combine(_root, "missing"), TypeName));
    }

    [Fact]
    public void Search_FindsMatchesInSubfolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Reports", "2026"));
        File.WriteAllText(Path.Combine(_root, "report.txt"), "");
        File.WriteAllText(Path.Combine(_root, "Reports", "2026", "Report Q1.docx"), "");
        File.WriteAllText(Path.Combine(_root, "Reports", "notes.txt"), "");

        var names = FolderReader.Search(_root, "report", TypeName).Select(i => i.Name).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["Report Q1.docx", "Reports", "report.txt"], names);
    }

    [Fact]
    public void Search_SkipsHiddenFolders()
    {
        string hidden = Directory.CreateDirectory(Path.Combine(_root, "Hidden")).FullName;
        File.SetAttributes(hidden, FileAttributes.Hidden | FileAttributes.Directory);
        File.WriteAllText(Path.Combine(hidden, "match.txt"), "");

        Assert.Empty(FolderReader.Search(_root, "match", TypeName));
    }

    [Fact]
    public void Search_DoesNotFollowJunctions()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "Folder")).FullName;
        File.WriteAllText(Path.Combine(folder, "match.txt"), "");
        Directory.CreateSymbolicLink(Path.Combine(folder, "Loop"), _root);

        var paths = FolderReader.Search(_root, "match", TypeName).Select(i => i.Path).ToList();

        Assert.Equal([Path.Combine(folder, "match.txt")], paths);
    }

    [Fact]
    public void Search_Cancelled_Throws()
    {
        File.WriteAllText(Path.Combine(_root, "other.txt"), "");

        Assert.Throws<OperationCanceledException>(() =>
            FolderReader.Search(_root, "match", TypeName, new CancellationToken(canceled: true)).ToList());
    }

    [Fact]
    public void FindFolder_ReturnsCasingOnDisk()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "Photos", "Summer")).FullName;
        string typed = char.ToLowerInvariant(folder[0]) + folder[1.._root.Length] + @"\photos\SUMMER";

        Assert.Equal(folder, FolderReader.FindFolder(typed));
    }

    [Fact]
    public void FindFolder_DriveRoot_UppercasesDriveLetter()
    {
        string root = Path.GetPathRoot(_root)!;

        Assert.Equal(root.ToUpperInvariant(), FolderReader.FindFolder(root.ToLowerInvariant()));
    }

    [Fact]
    public void FindFolder_MissingFolder_ReturnsNull()
    {
        Assert.Null(FolderReader.FindFolder(Path.Combine(_root, "missing")));
    }

    [Fact]
    public void FindFolder_File_ReturnsNull()
    {
        string file = Path.Combine(_root, "notes.txt");
        File.WriteAllText(file, "");

        Assert.Null(FolderReader.FindFolder(file));
    }

    [Fact]
    public void Read_Cancelled_Throws()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "");

        Assert.Throws<OperationCanceledException>(() => FolderReader.Read(_root, TypeName, new CancellationToken(canceled: true)));
    }
}
