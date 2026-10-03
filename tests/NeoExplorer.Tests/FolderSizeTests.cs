using System.Globalization;
using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public sealed class FolderSizeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("NeoExplorerTests").FullName;

    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Measure_CountsFilesAndFoldersInSubfolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "A", "B"));
        File.WriteAllBytes(Path.Combine(_root, "one.bin"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_root, "A", "two.bin"), new byte[5000]);
        File.WriteAllBytes(Path.Combine(_root, "A", "B", "three.bin"), []);

        FolderSizeInfo info = FolderSize.Measure(_root, clusterSize: 4096);

        Assert.Equal(new FolderSizeInfo(Files: 3, Folders: 2, Size: 5010, SizeOnDisk: 4096 + 8192), info);
    }

    [Fact]
    public void Measure_IncludesHiddenFiles()
    {
        string hidden = Path.Combine(_root, "hidden.txt");
        File.WriteAllText(hidden, "abc");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        Assert.Equal(1, FolderSize.Measure(_root, 4096).Files);
    }

    [Fact]
    public void Measure_EmptyFolder()
    {
        Assert.Equal(default, FolderSize.Measure(_root, 4096));
    }

    [Fact]
    public void Measure_StopsWhenCancelled()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => FolderSize.Measure(_root, 4096, cancellationToken: cancelled.Token));
    }
}

public class PropertiesFormatterTests
{
    [Fact]
    public void TypeOfFile_AddsTheExtension()
    {
        Assert.Equal("Text Document (.txt)", PropertiesFormatter.TypeOfFile("Text Document", ".txt"));
        Assert.Equal("File", PropertiesFormatter.TypeOfFile("File", ""));
    }

    [Fact]
    public void Date_IncludesTheDayAndSeconds()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal(
            "Saturday, October 3, 2026, 2:15:42 PM",
            PropertiesFormatter.Date(new DateTime(2026, 10, 3, 14, 15, 42), culture).Replace(' ', ' '));
    }

    [Fact]
    public void Contains_CountsFilesAndFolders()
    {
        Assert.Equal("1,234 Files, 1 Folders", PropertiesFormatter.Contains(1234, 1, CultureInfo.InvariantCulture));
    }
}
