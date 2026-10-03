namespace NeoExplorer.Core;

/// <summary>
/// A rectangle given by its edges, in DPI-independent pixels.
/// </summary>
public readonly record struct Bounds(double Left, double Top, double Right, double Bottom)
{
    /// <summary>
    /// The rectangle spanned by two corners, in any order, e.g. where a drag started and where the pointer is.
    /// </summary>
    public static Bounds FromCorners(double x1, double y1, double x2, double y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));

    public bool Intersects(Bounds other) =>
        Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
}

/// <summary>
/// Where the item containers of a list or grid view sit. Items fill rows left to right, <see cref="Columns"/>
/// per row; a list has one column. Each item has the same size and spacing, so the position of every item is
/// known, even one that is scrolled out of view and has no container.
/// </summary>
/// <param name="OriginX">Left edge of the first item.</param>
/// <param name="OriginY">Top edge of the first item.</param>
/// <param name="PitchX">Distance from one column to the next.</param>
/// <param name="PitchY">Distance from one row to the next.</param>
/// <param name="ItemWidth">Width of an item, at most <paramref name="PitchX"/>.</param>
/// <param name="ItemHeight">Height of an item, at most <paramref name="PitchY"/>.</param>
public sealed record ItemGrid(double OriginX, double OriginY, double PitchX, double PitchY, double ItemWidth, double ItemHeight, int Columns, int Count)
{
    /// <summary>
    /// A container that is on screen, at <paramref name="X"/>, <paramref name="Y"/> in the panel holding the items.
    /// </summary>
    public readonly record struct Sample(int Index, double X, double Y);

    public Bounds GetBounds(int index)
    {
        double left = OriginX + (index % Columns) * PitchX;
        double top = OriginY + (index / Columns) * PitchY;
        return new Bounds(left, top, left + ItemWidth, top + ItemHeight);
    }

    /// <summary>
    /// The indexes of the items that <paramref name="area"/> touches, in order.
    /// </summary>
    public IEnumerable<int> HitTest(Bounds area)
    {
        if (Count == 0 || PitchY <= 0)
        {
            yield break;
        }

        int firstRow = Math.Max(0, (int)Math.Floor((area.Top - OriginY) / PitchY));
        int lastRow = Math.Min((Count - 1) / Columns, (int)Math.Floor((area.Bottom - OriginY) / PitchY));
        int firstColumn = Columns == 1 ? 0 : Math.Max(0, (int)Math.Floor((area.Left - OriginX) / PitchX));
        int lastColumn = Columns == 1 ? 0 : Math.Min(Columns - 1, (int)Math.Floor((area.Right - OriginX) / PitchX));
        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                int index = row * Columns + column;
                if (index < Count && GetBounds(index).Intersects(area))
                {
                    yield return index;
                }
            }
        }
    }

    /// <summary>
    /// Works out the grid from the containers that are on screen, sorted by index.
    /// A grid view gives the cell size (<paramref name="pitchX"/>, <paramref name="pitchY"/>); for a list,
    /// pass null for both and the row spacing is measured from the containers.
    /// </summary>
    public static ItemGrid? FromSamples(IReadOnlyList<Sample> samples, double itemWidth, double itemHeight, double? pitchX, double? pitchY, int count)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        Sample first = samples[0];
        Sample last = samples[^1];

        if (pitchX is null || pitchY is null)
        {
            // A list: one column, rows spaced evenly.
            double pitch = last.Index > first.Index ? (last.Y - first.Y) / (last.Index - first.Index) : itemHeight;
            return new ItemGrid(first.X, first.Y - first.Index * pitch, itemWidth, pitch, itemWidth, itemHeight, 1, count);
        }

        double originX = samples.Min(s => s.X);
        int columns;
        Sample? otherRow =samples.Cast<Sample?>().LastOrDefault(s => s!.Value.Y - first.Y > pitchY.Value / 2);
        if (otherRow is not Sample other)
        {
            // Everything on screen fits in one row, so there is no second row.
            columns = Math.Max(1, count);
        }
        else
        {
            // other.Index - first.Index = rows * columns + (column difference).
            int rows = (int)Math.Round((other.Y - first.Y) / pitchY.Value);
            int columnDifference = (int)Math.Round((other.X - first.X) / pitchX.Value);
            columns = Math.Max(1, (other.Index - first.Index - columnDifference) / rows);
        }

        int firstColumn = (int)Math.Round((first.X - originX) / pitchX.Value);
        double originY = first.Y - ((first.Index - firstColumn) / columns) * pitchY.Value;
        return new ItemGrid(originX, originY, pitchX.Value, pitchY.Value, itemWidth, itemHeight, columns, count);
    }
}
