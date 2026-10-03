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
    public void GetSegments_NetworkShare_StartsWithNetworkAndComputer()
    {
        var segments = PathParser.GetSegments(@"\\server\share\folder");

        Assert.Equal(
            [
                new PathSegment("Network", "Network"),
                new PathSegment("server", @"\\server"),
                new PathSegment("share", @"\\server\share"),
                new PathSegment("folder", @"\\server\share\folder"),
            ],
            segments);
    }

    [Fact]
    public void GetSegments_NetworkComputer()
    {
        var segments = PathParser.GetSegments(@"\\server");

        Assert.Equal([new PathSegment("Network", "Network"), new PathSegment("server", @"\\server")], segments);
    }

    [Fact]
    public void GetSegments_Network_ReturnsOnlyNetwork()
    {
        Assert.Equal([new PathSegment("Network", "Network")], PathParser.GetSegments(PathParser.Network));
    }

    [Theory]
    [InlineData(@"\\server", true)]
    [InlineData(@"\\server\share", false)]
    [InlineData(@"\\server\", false)]
    [InlineData(@"\\", false)]
    [InlineData(@"\\?", false)]
    [InlineData(@"C:\", false)]
    [InlineData("Network", false)]
    public void IsNetworkComputer(string location, bool expected)
    {
        Assert.Equal(expected, PathParser.IsNetworkComputer(location));
    }

    [Theory]
    [InlineData("Network", true)]
    [InlineData(@"\\server", true)]
    [InlineData(@"\\server\share\folder", true)]
    [InlineData(@"C:\Users", false)]
    [InlineData("This PC", false)]
    public void IsNetworkLocation(string location, bool expected)
    {
        Assert.Equal(expected, PathParser.IsNetworkLocation(location));
    }

    [Fact]
    public void PathSegment_ToString_IsName()
    {
        Assert.Equal("Users", new PathSegment("Users", @"C:\Users").ToString());
    }

    [Theory]
    [InlineData(@"C:\Users\marti", @"C:\Users")]
    [InlineData(@"C:\Users", @"C:\")]
    [InlineData(@"C:\", PathParser.ThisPC)]
    [InlineData(@"\\server\share\folder", @"\\server\share")]
    [InlineData(@"\\server\share", @"\\server")]
    [InlineData(@"\\server", PathParser.Network)]
    public void GetParent(string location, string expected)
    {
        Assert.Equal(expected, PathParser.GetParent(location));
    }

    [Theory]
    [InlineData(PathParser.ThisPC)]
    [InlineData(PathParser.Network)]
    public void GetParent_TopLevel_IsNull(string location)
    {
        Assert.Null(PathParser.GetParent(location));
    }

    [Theory]
    [InlineData(@"C:\Users\", @"C:\Users")]
    [InlineData(@"  ""C:\Users""  ", @"C:\Users")]
    [InlineData(@"C:\Users\marti\..\Public", @"C:\Users\Public")]
    [InlineData(@"C:/Users/marti", @"C:\Users\marti")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData("this pc", PathParser.ThisPC)]
    [InlineData("network", PathParser.Network)]
    [InlineData(@"\\server", @"\\server")]
    [InlineData(@"\\server\", @"\\server")]
    [InlineData(@"\\server\share\", @"\\server\share")]
    [InlineData(@"\\server\share\folder\", @"\\server\share\folder")]
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
