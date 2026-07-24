using System.Runtime.InteropServices;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;

namespace ScreenStudio.Native.Capture;

/// <summary>WH_MOUSE_LL low-level mouse hook + GetCursorInfo polling. Captures cursor
/// position and button state, timestamped with QueryPerformanceCounter (converted to ms by
/// the supplied callback). Emits CursorEvent into the observable.</summary>
internal sealed class CursorHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private readonly LowLevelMouseProc _proc;
    private IntPtr _hook = IntPtr.Zero;
    private IObservable<CursorEvent>? _target;
    private Func<long, double>? _toMs;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    public CursorHook()
    {
        _proc = HookCallback;
    }

    public void Start(Func<long, double> toMs, IObservable<CursorEvent> target)
    {
        _toMs = toMs;
        _target = target;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(module.ModuleName!), 0);
        if (_hook == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_MOUSE_LL) failed");
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsExSafe(); _hook = IntPtr.Zero; }
    }

    private void UnhookWindowsExSafe()
    {
        try { if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook); } catch { }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _target is Subject<CursorEvent> sub && _toMs is not null)
        {
            int msg = wParam.ToInt32();
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            QueryPerformanceCounter(out long ticks);
            var tMs = _toMs(ticks);
            var buttons = ButtonsFor(msg);
            if (msg == WM_MOUSEMOVE || buttons != CursorButtonState.None)
            {
                sub.OnNext(new CursorEvent(tMs, new Vec2(info.pt.X, info.pt.Y), buttons));
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static CursorButtonState ButtonsFor(int msg) => msg switch
    {
        WM_LBUTTONDOWN or WM_LBUTTONUP => CursorButtonState.Left,
        WM_RBUTTONDOWN or WM_RBUTTONUP => CursorButtonState.Right,
        WM_MBUTTONDOWN or WM_MBUTTONUP => CursorButtonState.Middle,
        _ => CursorButtonState.None,
    };

    public void Dispose() => Stop();
}
