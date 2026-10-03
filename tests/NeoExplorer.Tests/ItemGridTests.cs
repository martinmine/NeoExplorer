using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class ItemGridTests
{
    // 4 columns of 100 x 150 cells, items 96 x 146, starting at (12, 8).
    private static readonly ItemGrid Grid = new(12, 8, 100, 150, 96, 146, 4, 10);

    // A list of 32 px rows, 600 px wide.
    private static readonly ItemGrid List = new(0, 0, 600, 32, 600, 32, 1, 50);

    [Fact]
    public void GetBounds_PlacesItemsInRows()
    {
        Assert.Equal(new Bounds(12, 8, 108, 154), Grid.GetBounds(0));
        Assert.Equal(new Bounds(312, 8, 408, 154), Grid.GetBounds(3));
        Assert.Equal(new Bounds(112, 158, 208, 304), Grid.GetBounds(5));
    }

    [Fact]
    public void HitTest_FindsItemsTheAreaTouches()
    {
        // From inside item 1 to inside item 6.
        Assert.Equal([1, 2, 5, 6], Grid.HitTest(Bounds.FromCorners(150, 50, 250, 200)));
    }

    [Fact]
    public void HitTest_IgnoresTheGapsBetweenItems()
    {
        // The 4 px gap between columns 0 and 1, and below row 0.
        Assert.Empty(Grid.HitTest(new Bounds(109, 10, 111, 100)));
        Assert.Empty(Grid.HitTest(new Bounds(20, 155, 100, 157)));
    }

    [Fact]
    public void HitTest_StopsAtTheLastItem()
    {
        // Rows 2 and 3 would hold items 8 to 15, but there are only 10 items.
        Assert.Equal([8, 9], Grid.HitTest(new Bounds(0, 320, 1000, 1000)));
    }

    [Fact]
    public void HitTest_ListSelectsWholeRows()
    {
        Assert.Equal([3, 4, 5], List.HitTest(Bounds.FromCorners(500, 100, 550, 170)));
    }

    [Fact]
    public void HitTest_AreaOutsideTheItems_FindsNothing()
    {
        Assert.Empty(List.HitTest(new Bounds(10, -50, 20, -1)));
        Assert.Empty(List.HitTest(new Bounds(10, 1700, 20, 1800)));
        Assert.Empty(new ItemGrid(0, 0, 100, 100, 100, 100, 3, 0).HitTest(new Bounds(0, 0, 50, 50)));
    }

    [Fact]
    public void FromSamples_List_MeasuresRowSpacing()
    {
        // Rows 20 to 22 on screen.
        var grid = ItemGrid.FromSamples([new(20, 4, 680), new(21, 4, 714), new(22, 4, 748)], 590, 32, null, null, 50)!;

        Assert.Equal(34, grid.PitchY);
        Assert.Equal(0, grid.OriginY);
        Assert.Equal(1, grid.Columns);
        Assert.Equal([20], grid.HitTest(new Bounds(100, 690, 101, 691)));
    }

    [Fact]
    public void FromSamples_Grid_WorksOutTheColumns()
    {
        // Items 6 to 13 of a 4-column grid, starting mid-row.
        var samples = Enumerable.Range(6, 8).Select(i => new ItemGrid.Sample(i, 12 + i % 4 * 100, 8 + i / 4 * 150)).ToList();

        var grid = ItemGrid.FromSamples(samples, 96, 146, 100, 150, 20)!;

        Assert.Equal(4, grid.Columns);
        Assert.Equal(12, grid.OriginX);
        Assert.Equal(8, grid.OriginY);
        Assert.Equal(Grid.GetBounds(5) with { }, grid.GetBounds(5));
    }

    [Fact]
    public void FromSamples_OneRow_UsesTheItemCountAsColumns()
    {
        var grid = ItemGrid.FromSamples([new(0, 12, 8), new(1, 112, 8)], 96, 146, 100, 150, 2)!;

        Assert.Equal(2, grid.Columns);
        Assert.Equal([0, 1], grid.HitTest(new Bounds(0, 0, 1000, 1000)));
    }

    [Fact]
    public void FromSamples_NoContainers_ReturnsNull()
    {
        Assert.Null(ItemGrid.FromSamples([], 96, 146, 100, 150, 0));
    }
}
