using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Timeline;

/// <summary>PRD P2 #7 — "Speed up/slow down sections." A speed-ramp segment: a time range
/// played back at a non-1.0 speed multiplier. E.g. speed=2.0 = 2x fast-forward, speed=0.5 =
/// slow-mo. The export pipeline uses this to remap output timestamps for the affected range.</summary>
public readonly record struct SpeedRamp(double StartMs, double EndMs, double Speed)
{
    public double DurationMs => System.Math.Max(0, EndMs - StartMs);
    /// <summary>Duration after speed adjustment (the output duration for this segment).</summary>
    public double AdjustedDurationMs => DurationMs / System.Math.Max(0.1, Speed);
    public bool Contains(double tMs) => tMs >= StartMs && tMs <= EndMs;
}

/// <summary>Manages a collection of speed ramps for a recording's timeline.</summary>
public sealed class SpeedRampCollection
{
    private readonly List<SpeedRamp> _ramps = new();
    public IReadOnlyList<SpeedRamp> Ramps => _ramps;

    public SpeedRampCollection Add(SpeedRamp ramp)
    {
        _ramps.Add(ramp);
        _ramps.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
        return this;
    }

    /// <summary>The total output duration after applying all speed ramps. Faster ramps shrink
    /// the output; slower ramps extend it.</summary>
    public double AdjustedDurationMs(double totalDurationMs)
    {
        var adjusted = totalDurationMs;
        foreach (var r in _ramps)
        {
            adjusted -= r.DurationMs;         // remove the original segment
            adjusted += r.AdjustedDurationMs; // add the speed-adjusted segment
        }
        return System.Math.Max(0, adjusted);
    }

    /// <summary>Remap a source timestamp to the output timeline given the speed ramps.</summary>
    public double RemapTime(double sourceMs)
    {
        var offset = 0.0;
        foreach (var r in _ramps)
        {
            if (sourceMs <= r.StartMs) break;
            if (sourceMs <= r.EndMs)
            {
                // Inside this ramp: map the portion through the speed multiplier.
                offset += (r.EndMs - r.StartMs) / r.Speed - r.DurationMs;
                break;
            }
            offset += r.AdjustedDurationMs - r.DurationMs;
        }
        return sourceMs + offset;
    }
}
