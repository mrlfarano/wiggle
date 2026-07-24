using ScreenStudio.Core.Math;
using SysMath = System.Math;

namespace ScreenStudio.Core.Zoom;

/// <summary>Resolved viewport transform at a given time, in source pixels.</summary>
public readonly record struct CameraState(double TimeMs, Vec2 Focus, double Scale)
{
    /// <summary>Map a source-pixel point into the zoomed viewport (0..1 coords).</summary>
    public Vec2 ToViewport(Vec2 sourcePoint, Vec2 sourceSize)
    {
        var rel = (sourcePoint - Focus);
        var n = new Vec2(rel.X / sourceSize.X, rel.Y / sourceSize.Y);
        return new(0.5 + n.X * Scale, 0.5 + n.Y * Scale);
    }
}

/// <summary>Task 004 — smooth zoom animation system. Evaluates a ZoomProgram at any time
/// with ease-in/out transitions between keyframes (deliverable: "smooth zoom transitions").
/// Uses cubic ease so zoom ramps feel professional rather than linear.</summary>
public static class ZoomCamera
{
    /// <summary>Evaluate the camera state at time t. Clamps to the first/last keyframe
    /// outside the program range; interpolates with ease-in/out between adjacent keys.</summary>
    public static CameraState Evaluate(ZoomProgram program, double timeMs)
    {
        var keys = program.Keyframes;
        if (keys.Count == 0) return new(timeMs, new(0, 0), 1.0);
        if (keys.Count == 1 || timeMs <= keys[0].TimeMs) return ToState(keys[0], timeMs);
        if (timeMs >= keys[^1].TimeMs) return ToState(keys[^1], timeMs);

        int i = 0;
        while (i < keys.Count - 1 && keys[i + 1].TimeMs < timeMs) i++;

        var a = keys[i];
        var b = keys[i + 1];
        var span = b.TimeMs - a.TimeMs;
        var u = span > 0 ? (timeMs - a.TimeMs) / span : 0.0;
        u = SysMath.Clamp(u, 0.0, 1.0);

        var eased = EaseInOutCubic(u);
        var rel = a.RelativeFocus.Lerp(b.RelativeFocus, eased);
        var scale = a.Scale + (b.Scale - a.Scale) * eased;
        return new(timeMs, rel, scale);
    }

    /// <summary>Resolve a CameraState's relative focus into absolute source pixels.</summary>
    public static CameraState WithAbsoluteFocus(this CameraState c, Vec2 sourceSize)
        => new(c.TimeMs, new(c.Focus.X * sourceSize.X, c.Focus.Y * sourceSize.Y), c.Scale);

    private static CameraState ToState(ZoomKeyframe k, double t)
        => new(t, k.RelativeFocus, k.Scale);

    /// <summary>Cubic ease-in/out: slow at both ends, fast in the middle.</summary>
    public static double EaseInOutCubic(double t)
        => t < 0.5 ? 4 * t * t * t : 1 - SysMath.Pow(-2 * t + 2, 3) / 2;
}
