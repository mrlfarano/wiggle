using System.Runtime.InteropServices;

namespace ScreenStudio.Native.Input;

/// <summary>A keyboard key event: which key (virtual-key code), its display name, whether it's
/// a modifier, and the timestamp. Used by the keycap-overlay feature (PRD P2 #2).</summary>
public readonly record struct KeyEvent(double TimestampMs, int VkCode, string DisplayName, bool IsModifier, bool IsKeyDown);

/// <summary>Task #019 — keyboard event capture via WH_KEYBOARD_LL low-level hook. Parallel to
/// CursorHook: captures key presses with timestamps for the keycap-display overlay. Emits
/// KeyEvent into an IObservable (the Subject pattern used throughout Native).</summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_KEYUP = 0x0101;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;
    private readonly Subject<KeyEvent> _events = new();

    public IObservable<KeyEvent> Events => _events;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public int vkCode; public int scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);

    private long _perfFreq;
    private long _startTicks;

    public KeyboardHook()
    {
        _proc = HookCallback;
        QueryPerformanceFrequency(out _perfFreq);
        QueryPerformanceCounter(out _startTicks);
    }

    public void Start()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName!), 0);
        if (_hook == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_KEYBOARD_LL) failed");
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            try { UnhookWindowsHookEx(_hook); } catch { }
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isKeyUp = msg == WM_KEYUP;
            if (isKeyDown || isKeyUp)
            {
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                QueryPerformanceCounter(out long t);
                var ms = (t - _startTicks) * 1000.0 / _perfFreq;
                var name = VkToName(info.vkCode, out bool isModifier);
                _events.OnNext(new KeyEvent(ms, info.vkCode, name, isModifier, isKeyDown));
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>Convert a virtual-key code to a human-readable display name (keycap text).</summary>
    private static string VkToName(int vk, out bool isModifier)
    {
        isModifier = vk is >= 0xA0 and <= 0xA5 or 0x10 or 0x11 or 0x12; // Shift/Ctrl/Alt + Win
        return vk switch
        {
            0x08 => "⌫", 0x09 => "⇥", 0x0D => "↵", 0x1B => "Esc",
            0x20 => "␣", 0x21 => "PgUp", 0x22 => "PgDn", 0x23 => "End", 0x24 => "Home",
            0x25 => "←", 0x26 => "↑", 0x27 => "→", 0x28 => "↓",
            0x10 => "Shift", 0x11 => "Ctrl", 0x12 => "Alt",
            0xA0 or 0xA1 => "Shift", 0xA2 or 0xA3 => "Ctrl", 0xA4 or 0xA5 => "Alt",
            0xBA => ";", 0xBB => "=", 0xBC => ",", 0xBD => "-", 0xBE => ".", 0xBF => "/",
            0xC0 => "`", 0xDB => "[", 0xDC => "\\", 0xDD => "]",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),    // 0-9
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),    // A-Z
            >= 0x70 and <= 0x7B => "F" + (vk - 0x6F),         // F1-F12
            _ => $"VK{vk:X2}",
        };
    }

    public void Dispose()
    {
        Stop();
        _events.Dispose();
    }
}
