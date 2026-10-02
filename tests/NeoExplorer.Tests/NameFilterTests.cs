using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class NameFilterTests
{
    [Theory]
    [InlineData("Report.docx", "port")]
    [InlineData("Report.docx", "REPORT")]
    [InlineData("Report.docx", "")]
    [InlineData("notes.txt", "*.txt")]
    [InlineData("notes.TXT", "*.txt")]
    [InlineData("file2.txt", "file?.txt")]
    public void Matches(string name, string query)
    {
        Assert.True(NameFilter.Matches(name, query));
    }

    [Theory]
    [InlineData("Report.docx", "notes")]
    [InlineData("notes.txt.bak", "*.txt")]
    [InlineData("file10.txt", "file?.txt")]
    public void DoesNotMatch(string name, string query)
    {
        Assert.False(NameFilter.Matches(name, query));
    }
}
