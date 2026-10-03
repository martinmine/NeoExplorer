using System.IO.Enumeration;

namespace NeoExplorer.Core;

/// <summary>
/// What a folder contains, as shown in its Properties window.
/// </summary>
public readonly record struct FolderSizeInfo(long Files, long Folders, long Size, long SizeOnDisk);

public static class FolderSize
{
    private const int ReportEvery = 500;

    // Properties counts hidden and system files too, unlike the folder view.
    private static readonly EnumerationOptions Options = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// Adds up everything in <paramref name="path"/> and its subfolders. Links and junctions are counted
    /// but not followed, so nothing is counted twice. Reports running totals now and then, so the window
    /// can count up while a large folder is measured. This is blocking; call it from a background thread.
    /// </summary>
    public static FolderSizeInfo Measure(string path, long clusterSize, IProgress<FolderSizeInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        var enumerable = new FileSystemEnumerable<(bool IsFolder, long Length)>(
            path,
            (ref FileSystemEntry entry) => (entry.IsDirectory, entry.Length),
            Options)
        {
            ShouldRecursePredicate = (ref FileSystemEntry entry) =>
                (entry.Attributes & FileAttributes.ReparsePoint) == 0,
        };

        FolderSizeInfo total = default;
        int sinceReport = 0;
        foreach ((bool isFolder, long length) in enumerable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total = isFolder
                ? total with { Folders = total.Folders + 1 }
                : total with
                {
                    Files = total.Files + 1,
                    Size = total.Size + length,
                    SizeOnDisk = total.SizeOnDisk + SizeFormatter.RoundUpToCluster(length, clusterSize),
                };

            if (++sinceReport == ReportEvery)
            {
                sinceReport = 0;
                progress?.Report(total);
            }
        }

        return total;
    }
}
