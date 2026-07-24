using ScreenStudio.Core.Zoom;

namespace ScreenStudio.Core.Timeline;

/// <summary>A cut/segment boundary in the timeline. The timeline is a list of kept
/// segments; everything between/around them is removed (cut).</summary>
public readonly record struct Segment(double StartMs, double EndMs)
{
    public double DurationMs => System.Math.Max(0, EndMs - StartMs);
    public bool Contains(double t) => t >= StartMs && t <= EndMs;
}

/// <summary>Task 005 — Timeline Editor model. Holds the recording's playable span, the
/// kept segments (after trims/cuts), the zoom program, and a playhead. Pure data + logic;
/// the WinUI canvas binds to this. Covers the editor deliverables:
///  - Timeline component (segments + total span)
///  - Trim/cut operations (AddSegment / RemoveRange / TrimEdges)
///  - Zoom keyframe editing (ZoomProgram passthrough + MoveKeyframe / RemoveKeyframe)
///  - Time-based navigation (Playhead, Clamp, Snap)</summary>
public sealed class RecordingTimeline
{
    /// <summary>Full recorded span [0, DurationMs].</summary>
    public double DurationMs { get; }

    /// <summary>Kept segments, sorted, non-overlapping. Defaults to one full-span segment.</summary>
    public IReadOnlyList<Segment> Segments => _segments;
    private readonly List<Segment> _segments;

    /// <summary>The zoom keyframes edited on this timeline.</summary>
    public ZoomProgram Zoom { get; }

    /// <summary>Current playhead position in ms (timeline time, clamped to Duration).</summary>
    public double PlayheadMs { get; private set; }

    public RecordingTimeline(double durationMs, ZoomProgram? zoom = null)
    {
        if (durationMs <= 0) throw new ArgumentOutOfRangeException(nameof(durationMs));
        DurationMs = durationMs;
        Zoom = zoom ?? new ZoomProgram();
        _segments = new List<Segment> { new(0, durationMs) };
    }

    // ---- Trim / cut ----

    /// <summary>Trim the start and end of the recording to [start, end]. Rebuilds segments.</summary>
    public void TrimEdges(double startMs, double endMs)
    {
        startMs = Clamp(startMs);
        endMs = Clamp(endMs);
        if (endMs <= startMs) throw new ArgumentException("end must be after start");
        _segments.Clear();
        _segments.Add(new Segment(startMs, endMs));
    }

    /// <summary>Cut (remove) a range from the timeline, splitting any segment it overlaps.</summary>
    public void Cut(double fromMs, double toMs)
    {
        fromMs = Clamp(fromMs);
        toMs = Clamp(toMs);
        if (toMs <= fromMs) return;

        var next = new List<Segment>();
        foreach (var s in _segments)
        {
            if (toMs <= s.StartMs || fromMs >= s.EndMs)
            {
                next.Add(s); // disjoint
                continue;
            }
            // Keep the portion before the cut.
            if (fromMs > s.StartMs) next.Add(new Segment(s.StartMs, fromMs));
            // Keep the portion after the cut.
            if (toMs < s.EndMs) next.Add(new Segment(toMs, s.EndMs));
        }
        _segments.Clear();
        _segments.AddRange(next.OrderBy(s => s.StartMs));
    }

    /// <summary>Total kept duration after cuts/trims.</summary>
    public double KeptDurationMs => _segments.Sum(s => s.DurationMs);

    // ---- Keyframe editing ----

    public void AddKeyframe(ZoomKeyframe k) => Zoom.Add(k);

    /// <summary>Move an existing keyframe to a new time/focus/scale (drag in the editor).</summary>
    public bool MoveKeyframe(double oldTimeMs, ZoomKeyframe newValue)
        => Zoom.ReplaceAt(oldTimeMs, newValue);

    public bool RemoveKeyframeAt(double timeMs)
        => Zoom.RemoveAt(timeMs);

    // ---- Navigation ----

    public void SetPlayhead(double tMs) => PlayheadMs = Clamp(tMs);

    /// <summary>Snap a time to the nearest keyframe within tolerance (for scrub-drag UX).</summary>
    public double SnapToKeyframe(double tMs, double toleranceMs = 100.0)
    {
        double best = tMs;
        double bestDist = toleranceMs;
        foreach (var k in Zoom.Keyframes)
        {
            var d = System.Math.Abs(k.TimeMs - tMs);
            if (d <= bestDist) { bestDist = d; best = k.TimeMs; }
        }
        return best;
    }

    public bool IsKept(double tMs) => _segments.Any(s => s.Contains(tMs));

    private double Clamp(double t) => System.Math.Clamp(t, 0, DurationMs);
}
