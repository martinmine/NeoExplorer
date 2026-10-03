using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public sealed class AttributeChangerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("NeoExplorerTests").FullName;

    public void Dispose()
    {
        AttributeChanger.Apply(_root, 0, FileAttributes.ReadOnly | FileAttributes.Hidden, includeContents: true);
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Change_SetsAndClears()
    {
        FileAttributes result = AttributeChanger.Change(FileAttributes.Archive | FileAttributes.Hidden, FileAttributes.ReadOnly, FileAttributes.Hidden);

        Assert.Equal(FileAttributes.Archive | FileAttributes.ReadOnly, result);
    }

    [Fact]
    public void Apply_ChangesOneFile()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "");

        Assert.Equal(0, AttributeChanger.Apply(file, FileAttributes.ReadOnly | FileAttributes.Hidden, 0, includeContents: false));

        Assert.True(File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly));
        Assert.True(File.GetAttributes(file).HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public void Apply_FolderOnly_LeavesContentsAlone()
    {
        string inner = Path.Combine(_root, "a.txt");
        File.WriteAllText(inner, "");

        AttributeChanger.Apply(_root, FileAttributes.Hidden, 0, includeContents: false);

        Assert.True(File.GetAttributes(_root).HasFlag(FileAttributes.Hidden));
        Assert.False(File.GetAttributes(inner).HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public void Apply_IncludingContents_ChangesSubfoldersAndFiles()
    {
        string sub = Directory.CreateDirectory(Path.Combine(_root, "Sub")).FullName;
        string deep = Path.Combine(sub, "deep.txt");
        File.WriteAllText(deep, "");
        File.SetAttributes(deep, FileAttributes.ReadOnly);

        AttributeChanger.Apply(_root, FileAttributes.Hidden, FileAttributes.ReadOnly, includeContents: true);

        Assert.True(File.GetAttributes(sub).HasFlag(FileAttributes.Hidden));
        Assert.Equal(FileAttributes.Hidden, File.GetAttributes(deep) & (FileAttributes.Hidden | FileAttributes.ReadOnly));
    }

    [Fact]
    public void Apply_MissingItem_CountsAsFailed()
    {
        Assert.Equal(1, AttributeChanger.Apply(Path.Combine(_root, "missing.txt"), FileAttributes.Hidden, 0, includeContents: false));
    }
}
