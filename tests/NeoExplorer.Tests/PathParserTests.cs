using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class PathParserTests
{
    [Fact]
    public void GetSegments_ThisPC_ReturnsOnlyThisPC()
    {
        var segments = PathParser.GetSegments(PathParser.ThisPC);

        Assert.Equal([new PathSegment("This PC", "This PC")], segments);
    }

    [Fact]
    public void GetSegments_DriveRoot()
    {
        var segments = PathParser.GetSegments(@"C:\");

        Assert.Equal(
            [new PathSegment("This PC", "This PC"), new PathSegment("C:", @"C:\")],
            segments);
    }

    [Fact]
    public void GetSegments_NestedFolder()
    {
        var segments = PathParser.GetSegments(@"C:\Users\marti\Documents");

        Assert.Equal(
            [
                new PathSegment("This PC", "This PC"),
                new PathSegment("C:", @"C:\"),
                new PathSegment("Users", @"C:\Users"),
                new PathSegment("marti", @"C:\Users\marti"),
                new PathSegment("Documents", @"C:\Users\marti\Documents"),
            ],
            segments);
    }

    [Fact]
    public void GetSegments_NetworkShare_UsesShareAsRoot()
    {
        var segments = PathParser.GetSegments(@"\\server\share\folder");

        Assert.Equal(
            [
                new PathSegment("This PC", "This PC"),
                new PathSegment(@"\\server\share", @"\\server\share"),
                new PathSegment("folder", @"\\server\share\folder"),
            ],
            segments);
    }

    [Theory]
    [InlineData(@"C:\Users\marti", @"C:\Users")]
    [InlineData(@"C:\Users", @"C:\")]
    [InlineData(@"C:\", PathParser.ThisPC)]
    public void GetParent(string location, string expected)
    {
        Assert.Equal(expected, PathParser.GetParent(location));
    }

    [Fact]
    public void GetParent_ThisPC_IsNull()
    {
        Assert.Null(PathParser.GetParent(PathParser.ThisPC));
    }

    [Theory]
    [InlineData(@"C:\Users\", @"C:\Users")]
    [InlineData(@"  ""C:\Users""  ", @"C:\Users")]
    [InlineData(@"C:\Users\marti\..\Public", @"C:\Users\Public")]
    [InlineData(@"C:/Users/marti", @"C:\Users\marti")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData("this pc", PathParser.ThisPC)]
    public void Normalize_ValidInput(string input, string expected)
    {
        Assert.Equal(expected, PathParser.Normalize(input));
    }

    [Fact]
    public void Normalize_ExpandsEnvironmentVariables()
    {
        string expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(expected, PathParser.Normalize("%USERPROFILE%"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Documents")]
    [InlineData(@"C:relative")]
    [InlineData(@"\Users")]
    public void Normalize_InvalidInput_ReturnsNull(string input)
    {
        Assert.Null(PathParser.Normalize(input));
    }
}
