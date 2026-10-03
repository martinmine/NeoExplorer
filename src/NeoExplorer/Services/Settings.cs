using NeoExplorer.Core;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace NeoExplorer.Services;

/// <summary>
/// Settings that are kept between runs, stored in the app's local settings.
/// </summary>
public static class Settings
{
    private static IPropertySet Values => ApplicationData.Current.LocalSettings.Values;

    public static ViewMode ViewMode
    {
        get => Get(nameof(ViewMode)) is int mode && Enum.IsDefined((ViewMode)mode) ? (ViewMode)mode : ViewMode.Details;
        set => Values[nameof(ViewMode)] = (int)value;
    }

    /// <summary>
    /// The window size when it was last closed without being maximized, in DPI-independent pixels.
    /// </summary>
    public static (double Width, double Height)? WindowSize
    {
        get => Get("WindowWidth") is double width && Get("WindowHeight") is double height ? (width, height) : null;
        set
        {
            Values["WindowWidth"] = value?.Width;
            Values["WindowHeight"] = value?.Height;
        }
    }

    public static bool IsMaximized
    {
        get => Get(nameof(IsMaximized)) is true;
        set => Values[nameof(IsMaximized)] = value;
    }

    private static object? Get(string key) => Values.TryGetValue(key, out object? value) ? value : null;
}
