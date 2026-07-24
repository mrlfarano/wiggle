using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Zoom;

/// <summary>A zoom camera state at an instant: where the viewport is centered
/// (Focus, in source-frame pixels) and how much it is zoomed (Scale, 1.0 = no zoom).
/// Keyframes are stored as RELATIVE focus positions (0..1 of source width/height) so
/// they survive an aspect-ratio change (task 009) without re-authoring.</summary>
public readonly record struct ZoomKeyframe(double TimeMs, Vec2 RelativeFocus, double Scale)
{
    /// <summary>Convert a relative focus to absolute source pixels for a given source size.</summary>
    public Vec2 AbsoluteFocus(Vec2 sourceSize) => new(RelativeFocus.X * sourceSize.X, RelativeFocus.Y * sourceSize.Y);

    public static ZoomKeyframe Default(double timeMs) => new(timeMs, new(0.5, 0.5), 1.0);
}

/// <summary>The full zoom program for a recording: an ordered list of keyframes.
/// Evaluated with smooth ease-in/out interpolation between keyframes (task 004).</summary>
public sealed class ZoomProgram
{
    private readonly List<ZoomKeyframe> _keys = new();
    public IReadOnlyList<ZoomKeyframe> Keyframes => _keys;

    public ZoomProgram Add(ZoomKeyframe k)
    {
        _keys.Add(k);
        _keys.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return this;
    }

    /// <summary>Replace the keyframe whose time is within 0.5ms of <paramref name="oldTimeMs"/>
    /// with <paramref name="newValue"/>. Returns false if no match found.</summary>
    public bool ReplaceAt(double oldTimeMs, ZoomKeyframe newValue)
    {
        int idx = _keys.FindIndex(k => System.Math.Abs(k.TimeMs - oldTimeMs) < 0.5);
        if (idx < 0) return false;
        _keys[idx] = newValue;
        _keys.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return true;
    }

    public bool RemoveAt(double timeMs)
    {
        int idx = _keys.FindIndex(k => System.Math.Abs(k.TimeMs - timeMs) < 0.5);
        if (idx < 0) return false;
        _keys.RemoveAt(idx);
        return true;
    }

    public ZoomProgram Clear() { _keys.Clear(); return this; }

    /// <summary>Number of keyframes (for timeline visualization).</summary>
    public int Count => _keys.Count;
}
