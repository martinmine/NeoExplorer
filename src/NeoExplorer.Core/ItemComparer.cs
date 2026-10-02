using System.Runtime.InteropServices;

namespace NeoExplorer.Core;

public enum SortColumn
{
    Name,
    DateModified,
    Type,
    Size,
}

/// <summary>
/// Sorts items like File Explorer: folders before files, then by the chosen column, then by name.
/// Descending order reverses the whole list, so folders end up last.
/// Names are compared the way Explorer does, so "file2" comes before "file10".
/// </summary>
public class ItemComparer(SortColumn column, bool descending) : IComparer<FileSystemItem>
{
    public int Compare(FileSystemItem? x, FileSystemItem? y)
    {
        if (x is null || y is null)
        {
            return Comparer<object>.Default.Compare(x, y);
        }

        int result = y.IsFolder.CompareTo(x.IsFolder);
        if (result == 0)
        {
            result = column switch
            {
                SortColumn.DateModified => x.DateModified.CompareTo(y.DateModified),
                SortColumn.Type => StringComparer.CurrentCultureIgnoreCase.Compare(x.Type, y.Type),
                SortColumn.Size => Nullable.Compare(x.Size, y.Size),
                _ => 0,
            };
        }

        if (result == 0)
        {
            result = CompareNames(x.Name, y.Name);
        }

        return descending ? -result : result;
    }

    public static int CompareNames(string x, string y) => StrCmpLogicalW(x, y);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string x, string y);
}
