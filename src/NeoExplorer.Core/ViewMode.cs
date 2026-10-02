namespace NeoExplorer.Core;

/// <summary>
/// How the folder content is shown, ordered from smallest to largest.
/// </summary>
public enum ViewMode
{
    Details,
    MediumIcons,
    LargeIcons,
    ExtraLargeIcons,
}

public static class ViewModes
{
    /// <summary>
    /// Steps to a larger (positive steps) or smaller (negative steps) view, stopping at the ends.
    /// </summary>
    public static ViewMode Zoom(ViewMode mode, int steps) =>
        (ViewMode)Math.Clamp((int)mode + steps, (int)ViewMode.Details, (int)ViewMode.ExtraLargeIcons);
}
