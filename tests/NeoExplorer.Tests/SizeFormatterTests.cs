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

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(999, "999 bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(126_464_000_000, "118 GB")]
    [InlineData(25_125_000_000, "23.4 GB")]
    [InlineData(999_653_638_144, "931 GB")]
    [InlineData(1_048_576_000_000, "977 GB")]
    [InlineData(1_073_741_824_000, "0.98 TB")]
    [InlineData(2_000_398_934_016, "1.82 TB")]
    public void FormatCompact_UsesAboutThreeSignificantDigits(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormatter.FormatCompact(bytes, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(12, "12 bytes (12 bytes)")]
    [InlineData(1_294_336, "1.23 MB (1,294,336 bytes)")]
    public void FormatWithBytes_AddsTheExactSize(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormatter.FormatWithBytes(bytes, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0, 4096, 0)]
    [InlineData(1, 4096, 4096)]
    [InlineData(4096, 4096, 4096)]
    [InlineData(4097, 4096, 8192)]
    [InlineData(4097, 0, 4097)]
    public void RoundUpToCluster(long bytes, long clusterSize, long expected)
    {
        Assert.Equal(expected, SizeFormatter.RoundUpToCluster(bytes, clusterSize));
    }

    [Theory]
    [InlineData(16, 4096, 0)]
    [InlineData(0, 4096, 0)]
    [InlineData(8192, 4096, 8192)]
    [InlineData(16, 0, 16)]
    public void SizeOnDisk_FilesInTheFileTableTakeNoSpace(long allocationSize, long clusterSize, long expected)
    {
        Assert.Equal(expected, SizeFormatter.SizeOnDisk(allocationSize, clusterSize));
    }

    [Fact]
    public void Format_UsesCultureGroupSeparator()
    {
        var norwegian = CultureInfo.GetCultureInfo("nb-NO");
        string separator = norwegian.NumberFormat.NumberGroupSeparator;

        Assert.Equal($"1{separator}234 KB", SizeFormatter.Format(1_263_616, norwegian));
    }
}
