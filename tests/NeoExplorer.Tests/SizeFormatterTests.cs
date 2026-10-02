using System.Globalization;
using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class SizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 KB")]
    [InlineData(1, "1 KB")]
    [InlineData(1024, "1 KB")]
    [InlineData(1025, "2 KB")]
    [InlineData(1_263_616, "1,234 KB")]
    [InlineData(5_368_709_120, "5,242,880 KB")]
    public void Format_RoundsUpToKilobytes(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormatter.Format(bytes, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_UsesCultureGroupSeparator()
    {
        var norwegian = CultureInfo.GetCultureInfo("nb-NO");
        string separator = norwegian.NumberFormat.NumberGroupSeparator;

        Assert.Equal($"1{separator}234 KB", SizeFormatter.Format(1_263_616, norwegian));
    }
}
