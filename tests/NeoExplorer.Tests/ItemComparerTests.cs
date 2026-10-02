using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class ItemComparerTests
{
    private static readonly FileSystemItem Photos = Folder("Photos", new DateTime(2026, 1, 1));
    private static readonly FileSystemItem Music = Folder("Music", new DateTime(2026, 3, 1));
    private static readonly FileSystemItem Notes = File("notes.txt", new DateTime(2026, 2, 1), 500, "Text Document");
    private static readonly FileSystemItem Report = File("Report.docx", new DateTime(2026, 4, 1), 100, "Microsoft Word Document");
    private static readonly FileSystemItem Archive = File("archive.zip", new DateTime(2025, 1, 1), 9000, "Compressed (zipped) Folder");

    private static readonly FileSystemItem[] Items = [Report, Photos, Notes, Archive, Music];

    private static FileSystemItem Folder(string name, DateTime modified) =>
        new(name, $@"C:\{name}", true, modified, null, "File folder");

    private static FileSystemItem File(string name, DateTime modified, long size, string type) =>
        new(name, $@"C:\{name}", false, modified, size, type);

    private static string[] Sort(SortColumn column, bool descending = false) =>
        Items.Order(new ItemComparer(column, descending)).Select(i => i.Name).ToArray();

    [Fact]
    public void Name_FoldersFirst_IgnoringCase()
    {
        Assert.Equal(["Music", "Photos", "archive.zip", "notes.txt", "Report.docx"], Sort(SortColumn.Name));
    }

    [Fact]
    public void Name_Descending_ReversesEverything()
    {
        Assert.Equal(["Report.docx", "notes.txt", "archive.zip", "Photos", "Music"], Sort(SortColumn.Name, descending: true));
    }

    [Fact]
    public void DateModified_FoldersFirst()
    {
        Assert.Equal(["Photos", "Music", "archive.zip", "notes.txt", "Report.docx"], Sort(SortColumn.DateModified));
    }

    [Fact]
    public void Type_FoldersFirst()
    {
        Assert.Equal(["Music", "Photos", "archive.zip", "Report.docx", "notes.txt"], Sort(SortColumn.Type));
    }

    [Fact]
    public void Size_FoldersFirst_ThenSmallest()
    {
        Assert.Equal(["Music", "Photos", "Report.docx", "notes.txt", "archive.zip"], Sort(SortColumn.Size));
    }

    [Fact]
    public void Size_Descending_LargestFirst_FoldersLast()
    {
        Assert.Equal(["archive.zip", "notes.txt", "Report.docx", "Photos", "Music"], Sort(SortColumn.Size, descending: true));
    }

    [Fact]
    public void CompareNames_SortsNumbersNaturally()
    {
        string[] names = ["file10.txt", "file2.txt", "file1.txt"];

        Assert.Equal(["file1.txt", "file2.txt", "file10.txt"], names.Order(Comparer<string>.Create(ItemComparer.CompareNames)));
    }
}
