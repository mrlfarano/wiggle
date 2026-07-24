using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using SysMath = System.Math;

namespace ScreenStudio.Core.Smoothing;

/// <summary>Smoothing intensity exposed to the user (PRD P0: "configurable smoothing intensity").
/// Maps to the centripetal Catmull-Rom alpha: alpha controls how much the curve
/// resists overshoot on sharp direction changes (higher alpha = more smoothing).</summary>
public enum SmoothingIntensity
{
    None = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
}

/// <summary>Maps a user-facing intensity to a concrete spline alpha.
/// None bypasses the spline entirely (pure linear interpolation).</summary>
public static class SmoothingTension
{
    public const double NoneAlpha = -1.0;     // sentinel: linear interpolation, no spline
    public const double LightAlpha = 0.25;
    public const double MediumAlpha = 0.5;    // canonical centripetal Catmull-Rom
    public const double HeavyAlpha = 0.75;

    public static double AlphaFor(SmoothingIntensity intensity) => intensity switch
    {
        SmoothingIntensity.None => NoneAlpha,
        SmoothingIntensity.Light => LightAlpha,
        SmoothingIntensity.Medium => MediumAlpha,
        SmoothingIntensity.Heavy => HeavyAlpha,
        _ => MediumAlpha,
    };
}

/// <summary>
/// Task 003 — cursor smoothing. Converts a stream of raw (jittery) cursor
/// samples into a smooth path using centripetal Catmull-Rom spline
/// interpolation (alpha-parametrized, via the Barry–Goldman algorithm),
/// while pinning click events to their exact recorded positions.
///
/// Deliverables covered:
///  - Smoothing algorithm module
///  - Configurable intensity (alpha parameter)
///  - Click event accuracy preserved (hard anchors)
///  - Frame generator producing fixed-rate (60fps) smoothed samples for preview/export
/// </summary>
public sealed class CursorSmoother
{
    private readonly double _alpha;

    public CursorSmoother(SmoothingIntensity intensity = SmoothingIntensity.Medium)
        => _alpha = SmoothingTension.AlphaFor(intensity);

    /// <summary>Build a smoother directly from an alpha value. alpha=-1 => linear;
    /// 0..1 => centripetal/chordal Catmull-Rom.</summary>
    public CursorSmoother(double alpha)
    {
        if (alpha != -1.0 && (alpha < 0 || alpha > 1))
            throw new ArgumentOutOfRangeException(nameof(alpha));
        _alpha = alpha;
    }

    /// <summary>
    /// Resample a raw cursor stream to a fixed frame rate (e.g. 60fps), interpolating
    /// between samples. Click events are injected as hard anchors at their exact
    /// timestamp/position so a click never drifts off its true location.
    /// </summary>
    /// <param name="raw">Raw events, must be sorted ascending by TimestampMs.</param>
    /// <param name="frameRateHz">Target output rate (60 for 60fps).</param>
    public IReadOnlyList<CursorEvent> Smooth(IReadOnlyList<CursorEvent> raw, double frameRateHz = 60.0)
    {
        if (raw.Count == 0) return Array.Empty<CursorEvent>();
        if (frameRateHz <= 0) throw new ArgumentOutOfRangeException(nameof(frameRateHz));
        if (raw.Count == 1) return new[] { raw[0] };

        var start = raw[0].TimestampMs;
        var end = raw[^1].TimestampMs;
        var dtMs = 1000.0 / frameRateHz;

        var result = new List<CursorEvent>((int)((end - start) / dtMs) + 4);

        // Index of click events, so we can snap frames to them exactly.
        var clickIndices = new HashSet<int>(
            Enumerable.Range(0, raw.Count).Where(i => raw[i].IsClick));
        var clickWindowMs = dtMs * 0.5; // a frame within half a step owns the click

        for (double t = start; t <= end + 1e-9; t += dtMs)
        {
            // Snap to a nearby click anchor: if a click is within half a step, emit
            // the click's exact position/buttons at this frame.
            var anchor = NearestClick(raw, clickIndices, t, clickWindowMs);
            if (anchor.HasValue)
            {
                result.Add(anchor.Value.WithTimestamp(t));
                continue;
            }

            var (seg, u) = LocateSegment(raw, t);
            var pos = Evaluate(raw, seg, u);
            var buttons = InterpolateButtons(raw, seg, u);
            result.Add(new CursorEvent(t, pos, buttons));
        }
        return result;
    }

    /// <summary>Find the raw click closest to time t within windowMs; returns null if none.</summary>
    private static CursorEvent? NearestClick(
        IReadOnlyList<CursorEvent> raw, HashSet<int> clickIndices, double t, double windowMs)
    {
        int best = -1;
        double bestDist = double.MaxValue;
        foreach (var i in clickIndices)
        {
            var d = System.Math.Abs(raw[i].TimestampMs - t);
            if (d <= windowMs && d < bestDist) { bestDist = d; best = i; }
        }
        return best < 0 ? null : raw[best];
    }

    /// <summary>Find index i such that raw[i].T &lt;= t &lt; raw[i+1].T, plus local u in [0,1).</summary>
    private static (int seg, double u) LocateSegment(IReadOnlyList<CursorEvent> raw, double t)
    {
        int i = 0;
        while (i < raw.Count - 2 && raw[i + 1].TimestampMs <= t) i++;
        var t0 = raw[i].TimestampMs;
        var t1 = raw[i + 1].TimestampMs;
        var u = t1 > t0 ? (t - t0) / (t1 - t0) : 0.0;
        return (i, SysMath.Clamp(u, 0.0, 1.0));
    }

    /// <summary>Button state for an interpolated frame. A button is "down" on an
    /// interpolated frame only when it is held across BOTH segment endpoints (a drag);
    /// an instantaneous click (button down at a single sample) therefore shows ONLY on
    /// its exact snapped anchor, never on neighbouring interpolated frames. This keeps a
    /// click pinned to one precise frame/position (deliverable: click accuracy preserved).</summary>
    private static CursorButtonState InterpolateButtons(IReadOnlyList<CursorEvent> raw, int seg, double u)
    {
        if (seg + 1 >= raw.Count) return CursorButtonState.None;
        return raw[seg].Buttons & raw[seg + 1].Buttons; // AND => only held-across-segment buttons
    }

    private Vec2 Evaluate(IReadOnlyList<CursorEvent> raw, int seg, double u)
    {
        var p0 = raw[System.Math.Max(0, seg - 1)].Position;
        var p1 = raw[seg].Position;
        var p2 = raw[seg + 1].Position;
        var p3 = raw[System.Math.Min(raw.Count - 1, seg + 2)].Position;

        return _alpha == SmoothingTension.NoneAlpha
            ? p1.Lerp(p2, u)                                  // None: linear
            : CentripetalCatmullRom(p0, p1, p2, p3, u, _alpha); // spline
    }

    /// <summary>Centripetal/chordal/uniform Catmull-Rom via the Barry–Goldman algorithm.
    /// alpha: 0 = uniform, 0.5 = centripetal (default), 1 = chordal.
    /// Guarantees f(0)=p1, f(1)=p2 and C1 continuity. Centripetal (alpha=0.5) avoids
    /// the cusps/overshoot that uniform CR produces on sharp direction changes.</summary>
    internal static Vec2 CentripetalCatmullRom(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, double u, double alpha)
    {
        var d1 = Dist(p0, p1, alpha);
        var d2 = Dist(p1, p2, alpha);
        var d3 = Dist(p2, p3, alpha);

        var t0 = 0.0;
        var t1 = t0 + d1;
        var t2 = t1 + d2;
        var t3 = t2 + d3;
        if (t1 == t2) return p1; // zero-length segment guard

        var t = t1 + u * (t2 - t1);

        // Barry–Goldman nested linear interpolations.
        var L01 = (t1 - t0) == 0 ? p1 : Lerp2(p0, p1, (t - t0) / (t1 - t0));
        var L12 = (t2 - t1) == 0 ? p1 : Lerp2(p1, p2, (t - t1) / (t2 - t1));
        var L23 = (t3 - t2) == 0 ? p2 : Lerp2(p2, p3, (t - t2) / (t3 - t2));

        var C12 = (t2 - t0) == 0 ? L12 : Lerp2(L01, L12, (t - t0) / (t2 - t0));
        var C23 = (t3 - t1) == 0 ? L12 : Lerp2(L12, L23, (t - t1) / (t3 - t1));

        return Lerp2(C12, C23, (t - t1) / (t2 - t1));
    }

    private static double Dist(Vec2 a, Vec2 b, double alpha)
        => SysMath.Pow((b - a).Length, alpha);

    private static Vec2 Lerp2(Vec2 a, Vec2 b, double t) => a + (b - a) * t;
}

internal static class CursorEventExtensions
{
    public static CursorEvent WithTimestamp(this CursorEvent e, double tMs)
        => e with { TimestampMs = tMs };
}
