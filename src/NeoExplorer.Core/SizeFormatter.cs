using System.Globalization;

namespace NeoExplorer.Core;

public static class SizeFormatter
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Formats a file size like the File Explorer Details view: always in KB, rounded up ("1 KB", "1,234 KB").
    /// </summary>
    public static string Format(long bytes, IFormatProvider? provider = null)
    {
        long kilobytes = (bytes + 1023) / 1024;
        return string.Format(provider ?? CultureInfo.CurrentCulture, "{0:N0} KB", kilobytes);
    }

    /// <summary>
    /// Formats a size with about three significant digits, like drive space in File Explorer ("117 GB", "1.81 TB").
    /// </summary>
    public static string FormatCompact(long bytes, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        if (bytes < 1000)
        {
            return string.Format(provider, "{0} bytes", bytes);
        }

        double value = bytes / 1024.0;
        int unit = 0;
        while (value >= 1000 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string format = value < 10 ? "{0:0.##} {1}" : value < 100 ? "{0:0.#} {1}" : "{0:0} {1}";
        return string.Format(provider, format, value, Units[unit]);
    }

    /// <summary>
    /// Formats a size like the Properties window: compact, then the exact number of bytes ("1.23 MB (1,294,336 bytes)").
    /// Zero is just "0 bytes".
    /// </summary>
    public static string FormatWithBytes(long bytes, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        return bytes == 0 ? FormatCompact(0, provider) : string.Format(provider, "{0} ({1:N0} bytes)", FormatCompact(bytes, provider), bytes);
    }

    /// <summary>
    /// The space a file takes on disk: whole clusters, so a 1-byte file takes a full 4 KB cluster.
    /// </summary>
    public static long RoundUpToCluster(long bytes, long clusterSize) =>
        clusterSize <= 0 ? bytes : (bytes + clusterSize - 1) / clusterSize * clusterSize;

    /// <summary>
    /// The size on disk File Explorer shows for a file the file system reports <paramref name="allocationSize"/> for.
    /// NTFS keeps very small files inside its file table and reports a few bytes that aren't whole clusters;
    /// those take no space of their own, so Explorer shows 0 bytes.
    /// </summary>
    public static long SizeOnDisk(long allocationSize, long clusterSize) =>
        clusterSize > 0 && allocationSize % clusterSize != 0 ? 0 : allocationSize;
}
