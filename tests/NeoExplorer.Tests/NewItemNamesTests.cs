using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class NewItemNamesTests
{
    [Fact]
    public void Unique_KeepsAFreeName()
    {
        Assert.Equal("New folder", NewItemNames.Unique(NewItemNames.Folder, isFolder: true, _ => false));
    }

    [Fact]
    public void Unique_NumbersFoldersFromTwo()
    {
        HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase) { "new folder", "New folder (2)" };
        Assert.Equal("New folder (3)", NewItemNames.Unique(NewItemNames.Folder, isFolder: true, taken.Contains));
    }

    [Fact]
    public void Unique_PutsTheNumberBeforeTheExtension()
    {
        HashSet<string> taken = ["New Text Document.txt"];
        Assert.Equal("New Text Document (2).txt", NewItemNames.Unique(NewItemNames.TextDocument, isFolder: false, taken.Contains));
    }

    [Fact]
    public void Unique_TreatsDotsInFolderNamesAsPartOfTheName()
    {
        HashSet<string> taken = ["v1.2"];
        Assert.Equal("v1.2 (2)", NewItemNames.Unique("v1.2", isFolder: true, taken.Contains));
    }
}
