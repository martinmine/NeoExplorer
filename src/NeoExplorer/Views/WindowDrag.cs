using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace NeoExplorer.Views;

/// <summary>
/// Moves the window from a press outside the title bar, as if the title bar itself had been pressed.
/// </summary>
internal static class WindowDrag
{
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const nint HTCAPTION = 2;

    /// <summary>
    /// Hands the pressed mouse button over to Windows' own window move, which also snaps and restores
    /// a maximized window, until the button is released.
    /// </summary>
    public static void Start(UIElement element)
    {
        if (element.XamlRoot?.ContentIslandEnvironment is not { } environment)
        {
            return;
        }

        nint hwnd = Win32Interop.GetWindowFromWindowId(environment.AppWindowId);
        ReleaseCapture();
        SendMessageW(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessageW(nint hwnd, uint msg, nint wParam, nint lParam);
}
