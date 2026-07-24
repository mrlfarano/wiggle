using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Cursor;

/// <summary>Raw cursor sample as captured by the recording engine
/// (WH_MOUSE_LL hook / GetCursorInfo polling), timestamped with
/// QueryPerformanceCounter-derived ticks for later processing.</summary>
public readonly record struct CursorEvent(double TimestampMs, Vec2 Position, CursorButtonState Buttons)
{
    public bool IsClick => Buttons != CursorButtonState.None;
}

[Flags]
public enum CursorButtonState : byte
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
}
