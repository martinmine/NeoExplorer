using System.IO.Enumeration;

namespace NeoExplorer.Core;

public static class NameFilter
{
    /// <summary>
    /// Whether a file name matches a search, ignoring case. Plain text matches anywhere in the name;
    /// text with * or ? is a wildcard pattern for the whole name, e.g. "*.txt".
    /// An empty query matches everything.
    /// </summary>
    public static bool Matches(ReadOnlySpan<char> name, string query)
    {
        if (query.AsSpan().IndexOfAny('*', '?') >= 0)
        {
            return FileSystemName.MatchesSimpleExpression(query, name, ignoreCase: true);
        }

        return name.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
