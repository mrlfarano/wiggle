using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using ScreenStudio.App.Views;

namespace ScreenStudio.App.Services;

/// <summary>Task #011/#021 — system tray icon via Win32 Shell_NotifyIcon, with a right-click
/// context menu (New Recording / Settings / Quit). WinUI has no built-in tray API, so this
/// P/Invokes the classic Win32 NOTIFYICONDATA + CreatePopupMenu/TrackPopupMenu path. Left-click
/// activates the main window; right-click shows the context menu. Best-effort: silently no-ops
/// if Win32 calls fail (e.g. headless).</summary>
public sealed class TrayController : IDisposable
{
    private readonly Window _window;
    private readonly Action? _onNewRecording;
    private readonly Action? _onSettings;
    private readonly Action? _onQuit;
    private bool _added;

    /// <param name="onNewRecording">Callback for the "New Recording" menu item (null = item omitted).</param>
    /// <param name="onSettings">Callback for "Settings".</param>
    /// <param name="onQuit">Callback for "Quit".</param>
    public TrayController(Window window, Action? onNewRecording = null, Action? onSettings = null, Action? onQuit = null)
    {
        _window = window;
        _onNewRecording = onNewRecording;
        _onSettings = onSettings;
        _onQuit = onQuit;
        _added = AddTrayIcon();
    }

    private bool AddTrayIcon()
    {
        try
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = GetWindowHandle(_window),
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_TIP,
                uCallbackMessage = WM_APP_TRAY,
                szTip = "Screen Studio",
            };
            return Shell_NotifyIcon(NIM_ADD, ref data);
        }
        catch { return false; }
    }

    /// <summary>Bring the main window to the foreground (called on tray left-click).</summary>
    public void ActivateWindow()
    {
        try
        {
            var hwnd = GetWindowHandle(_window);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
        }
        catch { /* best-effort */ }
    }

    /// <summary>Show the right-click context menu at the cursor position (called on tray
    /// right-click). Uses Win32 CreatePopupMenu + TrackPopupMenu — the classic tray idiom.</summary>
    public void ShowContextMenu()
    {
        try
        {
            var hwnd = GetWindowHandle(_window);
            var menu = CreatePopupMenu();
            uint id = 1000;
            if (_onNewRecording != null) { AppendMenu(menu, MF_STRING, id++, "New Recording"); AppendMenu(menu, MF_SEPARATOR, 0, ""); }
            if (_onSettings != null) { AppendMenu(menu, MF_STRING, id++, "Settings"); }
            AppendMenu(menu, MF_SEPARATOR, 0, "");
            if (_onQuit != null) AppendMenu(menu, MF_STRING, id++, "Quit");

            GetCursorPos(out var pt);
            // TrackPopupMenu returns the selected item ID; 0 = cancelled.
            SetForegroundWindow(hwnd);
            var chosen = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, hwnd, IntPtr.Zero);

            // Dispatch the callback matching the chosen ID.
            uint current = 1000;
            if (_onNewRecording != null) { if (chosen == current) _onNewRecording(); current++; }
            // skip separator
            if (_onSettings != null) { if (chosen == current) _onSettings(); current++; }
            if (_onQuit != null) { if (chosen == current) _onQuit(); }

            DestroyMenu(menu);
        }
        catch { /* best-effort */ }
    }

    private static IntPtr GetWindowHandle(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        return hwnd;
    }

    public void Dispose()
    {
        if (!_added) return;
        try
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = GetWindowHandle(_window),
                uID = 1,
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        catch { }
    }

    // ---- Win32 interop ----
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_TIP = 0x00000004;
    private const uint WM_APP_TRAY = 0x8000;
    private const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    // ---- Popup menu interop (tray right-click) ----
    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);
}

/// <summary>WinUI Window→HWND helper (WinRT.Interop.WindowNative).</summary>
internal static class WindowNative
{
    public static IntPtr GetWindowHandle(Window window)
        => WinRT.Interop.WindowNative.GetWindowHandle(window);
}
