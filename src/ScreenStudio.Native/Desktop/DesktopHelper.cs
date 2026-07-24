using System.Runtime.InteropServices;

namespace ScreenStudio.Native.Desktop;

/// <summary>PRD P2 #7 — "Hide desktop icons." Toggles the Windows desktop icon visibility
/// via the registry key that Explorer reads (HKCU\Software\Microsoft\Windows\CurrentVersion\
/// Explorer\Advanced\HideIcons). Call HideIcons() before recording, RestoreIcons() after.</summary>
public static class DesktopHelper
{
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string RegValue = "HideIcons";
    private static int? _originalValue;

    /// <summary>Hide desktop icons (sets HideIcons=1, refreshes Explorer).</summary>
    public static void HideIcons()
    {
        _originalValue = ReadHideIcons();
        WriteHideIcons(1);
        RefreshDesktop();
    }

    /// <summary>Restore desktop icons to their pre-recording state.</summary>
    public static void RestoreIcons()
    {
        WriteHideIcons(_originalValue ?? 0);
        RefreshDesktop();
    }

    private static int? ReadHideIcons()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegPath);
        return key?.GetValue(RegValue) as int?;
    }

    private static void WriteHideIcons(int value)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegPath);
        key?.SetValue(RegValue, value, Microsoft.Win32.RegistryValueKind.DWord);
    }

    /// <summary>Send the Explorer a "refresh" notification so the icon toggle takes effect
    /// immediately without restarting Explorer.</summary>
    private static void RefreshDesktop()
    {
        // PostMessage to the Progman window to force a desktop refresh.
        var hwnd = FindWindow("Progman", null);
        if (hwnd != IntPtr.Zero)
            PostMessage(hwnd, WM_COMMAND, 0x7102, IntPtr.Zero); // refresh command
    }

    private const uint WM_COMMAND = 0x0111;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
