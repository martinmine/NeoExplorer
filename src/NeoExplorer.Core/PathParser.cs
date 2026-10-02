namespace NeoExplorer.Core;

/// <summary>
/// One part of a path as shown in the breadcrumb bar, e.g. "Users" for C:\Users.
/// </summary>
public record PathSegment(string Name, string Path);

public static class PathParser
{
    /// <summary>
    /// The location for the "This PC" view. Real paths are always rooted, so this never clashes with a folder.
    /// </summary>
    public const string ThisPC = "This PC";

    /// <summary>
    /// Splits a location into breadcrumb segments, starting with This PC.
    /// </summary>
    public static IReadOnlyList<PathSegment> GetSegments(string location)
    {
        var segments = new List<PathSegment> { new(ThisPC, ThisPC) };
        if (location == ThisPC)
        {
            return segments;
        }

        string root = Path.GetPathRoot(location)!;
        string rootName = root.TrimEnd(Path.DirectorySeparatorChar);
        segments.Add(new PathSegment(rootName, root));

        string current = root;
        foreach (string part in location[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            segments.Add(new PathSegment(part, current));
        }

        return segments;
    }

    /// <summary>
    /// Returns the parent location. The parent of a drive root is This PC, and This PC has no parent.
    /// </summary>
    public static string? GetParent(string location)
    {
        if (location == ThisPC)
        {
            return null;
        }

        return Path.GetDirectoryName(location) ?? ThisPC;
    }

    /// <summary>
    /// Turns a path typed by the user into a full path, or returns null if it is not a valid absolute path.
    /// Handles surrounding quotes, environment variables (%USERPROFILE%) and trailing separators.
    /// </summary>
    public static string? Normalize(string input)
    {
        string path = Environment.ExpandEnvironmentVariables(input.Trim().Trim('"'));
        if (path.Equals(ThisPC, StringComparison.OrdinalIgnoreCase))
        {
            return ThisPC;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return null;
        }

        try
        {
            path = Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return null;
        }

        string root = Path.GetPathRoot(path)!;
        return path.Length > root.Length ? Path.TrimEndingDirectorySeparator(path) : root;
    }
}
