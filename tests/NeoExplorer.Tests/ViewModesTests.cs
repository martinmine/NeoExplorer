using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class ViewModesTests
{
    [Theory]
    [InlineData(ViewMode.Details, 1, ViewMode.MediumIcons)]
    [InlineData(ViewMode.MediumIcons, 2, ViewMode.ExtraLargeIcons)]
    [InlineData(ViewMode.LargeIcons, -1, ViewMode.MediumIcons)]
    [InlineData(ViewMode.ExtraLargeIcons, 1, ViewMode.ExtraLargeIcons)]
    [InlineData(ViewMode.Details, -1, ViewMode.Details)]
    [InlineData(ViewMode.LargeIcons, -10, ViewMode.Details)]
    public void Zoom_StepsAndStopsAtEnds(ViewMode mode, int steps, ViewMode expected)
    {
        Assert.Equal(expected, ViewModes.Zoom(mode, steps));
    }
}
