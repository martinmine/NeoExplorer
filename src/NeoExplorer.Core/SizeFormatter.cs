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
}
