using System.Globalization;

namespace NeoExplorer.Core;

/// <summary>
/// Texts for the General tab of the Properties window, worded like File Explorer's.
/// </summary>
public static class PropertiesFormatter
{
    /// <summary>
    /// "Text Document (.txt)", or just the type name for a file without an extension.
    /// </summary>
    public static string TypeOfFile(string typeName, string extension) =>
        extension.Length == 0 || extension == "." ? typeName : $"{typeName} ({extension})";

    /// <summary>
    /// A date with the day of the week and the time to the second, e.g. "Saturday, October 3, 2026, 2:15:42 PM".
    /// </summary>
    public static string Date(DateTime date, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        return string.Format(provider, "{0:D}, {0:T}", date);
    }

    /// <summary>
    /// "12 Files, 3 Folders". File Explorer doesn't change the wording for one item.
    /// </summary>
    public static string Contains(long files, long folders, IFormatProvider? provider = null) =>
        string.Format(provider ?? CultureInfo.CurrentCulture, "{0:N0} Files, {1:N0} Folders", files, folders);
}
