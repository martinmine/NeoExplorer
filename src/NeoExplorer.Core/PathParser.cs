namespace NeoExplorer.Core;

/// <summary>
/// One part of a path as shown in the breadcrumb bar, e.g. "Users" for C:\Users.
/// </summary>
public record PathSegment(string Name, string Path)
{
    // The breadcrumb bar displays items using ToString().
    public override string ToString() => Name;
}

public static class PathParser
{
    /// <summary>
    /// The location for the "This PC" view. Real paths are always rooted, so this never clashes with a folder.
    /// </summary>
    public const string ThisPC = "This PC";

    /// <summary>
    /// The location listing the computers on the network. Like <see cref="ThisPC"/>, never a real path.
    /// </summary>
    public const string Network = "Network";

    private const string UncPrefix = @"\\";

    /// <summary>
    /// Splits a location into breadcrumb segments, starting with This PC, or with Network for network paths.
    /// </summary>
    public static IReadOnlyList<PathSegment> GetSegments(string location)
    {
        if (location == ThisPC)
        {
            return [new(ThisPC, ThisPC)];
        }

        var segments = new List<PathSegment>();
        string root;
        if (IsNetworkLocation(location))
        {
            segments.Add(new(Network, Network));
            if (location == Network)
            {
                return segments;
            }

            string computer = GetComputer(location);
            segments.Add(new(computer[UncPrefix.Length..], computer));
            if (location.Length == computer.Length)
            {
                return segments;
            }

            root = Path.GetPathRoot(location)!;
            segments.Add(new(root[(computer.Length + 1)..].TrimEnd(Path.DirectorySeparatorChar), root));
        }
        else
        {
            segments.Add(new(ThisPC, ThisPC));
            root = Path.GetPathRoot(location)!;
            segments.Add(new(root.TrimEnd(Path.DirectorySeparatorChar), root));
        }

        string current = root;
        foreach (string part in location[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            segments.Add(new PathSegment(part, current));
        }

        return segments;
    }

    /// <summary>
    /// Returns the parent location. The parent of a drive root is This PC, a shared folder's parent is its
    /// computer, a computer's parent is Network, and This PC and Network have no parent.
    /// </summary>
    public static string? GetParent(string location)
    {
        if (location is ThisPC or Network)
        {
            return null;
        }

        if (IsNetworkComputer(location))
        {
            return Network;
        }

        return Path.GetDirectoryName(location) ?? (IsNetworkLocation(location) ? GetComputer(location) : ThisPC);
    }

    /// <summary>
    /// True for a computer on the network, written like "\SERVER". Its shared folders are paths below it.
    /// </summary>
    public static bool IsNetworkComputer(string location)
    {
        if (!location.StartsWith(UncPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string name = location[UncPrefix.Length..];
        return name.Length > 0 && name is not ("?" or ".") && name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
    }

    /// <summary>
    /// True for Network, a network computer, or a path on a network share.
    /// </summary>
    public static bool IsNetworkLocation(string location) =>
        location == Network || location.StartsWith(UncPrefix, StringComparison.Ordinal);

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

        if (path.Equals(Network, StringComparison.OrdinalIgnoreCase))
        {
            return Network;
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

        // "\\server\" counts as a root, so TrimEndingDirectorySeparator leaves it alone.
        string computer = path.TrimEnd(Path.DirectorySeparatorChar);
        if (IsNetworkComputer(computer))
        {
            return computer;
        }

        string root = Path.GetPathRoot(path)!;
        return path.Length > root.Length ? Path.TrimEndingDirectorySeparator(path) : root;
    }

    // "\server\share\folder" becomes "\server".
    private static string GetComputer(string location)
    {
        int end = location.IndexOf(Path.DirectorySeparatorChar, UncPrefix.Length);
        return end < 0 ? location : location[..end];
    }
}
