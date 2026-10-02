using System.Globalization;

namespace NeoExplorer.Core;

public static class SizeFormatter
{
    /// <summary>
    /// Formats a file size like the File Explorer Details view: always in KB, rounded up ("1 KB", "1,234 KB").
    /// </summary>
    public static string Format(long bytes, IFormatProvider? provider = null)
    {
        long kilobytes = (bytes + 1023) / 1024;
        return string.Format(provider ?? CultureInfo.CurrentCulture, "{0:N0} KB", kilobytes);
    }
}
