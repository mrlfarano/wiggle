using ScreenStudio.Core.Audio;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;

namespace ScreenStudio.E2E.Tests.Fixtures;

/// <summary>Scripted cursor activity for deterministic test recordings.</summary>
public sealed class CursorScript
{
    public int SampleRateHz { get; set; } = 60;
    public double DurationSeconds { get; set; } = 2.0;
    public Vec2 Start { get; set; } = new(100, 100);
    public Vec2 End { get; set; } = new(800, 600);
    /// <summary>(timeMs, position) pairs for clicks injected into the path.</summary>
    public List<(double timeMs, Vec2 pos)> Clicks { get; } = new();
    /// <summary>(timeMs, durationMs) pairs for pauses (held cursor) injected into the path.</summary>
    public List<(double timeMs, double durationMs)> Pauses { get; } = new();
    /// <summary>Micro-jitter amplitude to give the smoother something to smooth (px).</summary>
    public double Jitter { get; set; } = 3.0;
    public int Seed { get; set; } = 1;
}
