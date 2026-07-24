using System.Runtime.InteropServices;
using System.Text;

namespace ScreenStudio.App.Services;

/// <summary>Task #015 (gap fix) — on-screen window enumeration for the capture-target picker
/// (ui-prd §9.3). Win32 EnumWindows + GetWindowText, filtered to visible, titled, non-cloaked
/// top-level windows. Used by the "Window" capture target in the RecordingOverlay.</summary>
public static class WindowEnumeration
{
    /// <summary>A capturable window: its title + HWND (for CaptureOptions.TargetHandle).</summary>
    public sealed record WindowTarget(string Title, IntPtr Handle);

    /// <summary>Enumerate visible top-level windows with titles. Empty if none / headless.</summary>
    public static List<WindowTarget> ListWindows()
    {
        var result = new List<WindowTarget>();
        try
        {
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                if (IsIconic(hwnd)) return true; // skip minimized

                var title = GetWindowText(hwnd);
                if (string.IsNullOrWhiteSpace(title)) return true;
                if (title == "Program Manager" || title == "Windows Input Experience") return true;

                result.Add(new WindowTarget(title, hwnd));
                return true;
            }, IntPtr.Zero);
        }
        catch { /* best-effort */ }
        return result;
    }

    private static string GetWindowText(IntPtr hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0) return "";
        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    // ---- Win32 ----
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
