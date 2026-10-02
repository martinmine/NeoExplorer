using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public sealed class FolderReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("NeoExplorerTests").FullName;

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

        var items = FolderReader.Read(_root).OrderBy(i => i.Name).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(new FileSystemItem("notes.txt", file, false, modified, 5), items[0]);
        Assert.Equal("Photos", items[1].Name);
        Assert.Equal(folder, items[1].Path);
        Assert.True(items[1].IsFolder);
        Assert.Null(items[1].Size);
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

        var items = FolderReader.Read(_root);

        Assert.Equal("visible.txt", Assert.Single(items).Name);
    }

    [Fact]
    public void Read_EmptyFolder_ReturnsEmptyList()
    {
        Assert.Empty(FolderReader.Read(_root));
    }

    [Fact]
    public void Read_MissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => FolderReader.Read(Path.Combine(_root, "missing")));
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

        Assert.Throws<OperationCanceledException>(() => FolderReader.Read(_root, new CancellationToken(canceled: true)));
    }
}
