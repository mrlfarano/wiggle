using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Smoothing;

namespace ScreenStudio.Core.Tests.Smoothing;

public class CursorSmootherTests
{
    private static CursorEvent E(double tMs, double x, double y, CursorButtonState b = CursorButtonState.None)
        => new(tMs, new Vec2(x, y), b);

    private static List<CursorEvent> JitteredLine(int n, double dtMs = 16.6, double jitter = 4.0, int seed = 1)
    {
        var rng = new Random(seed);
        var list = new List<CursorEvent>(n);
        for (int i = 0; i < n; i++)
            list.Add(E(i * dtMs, i * 10, 100 + (rng.NextDouble() - 0.5) * jitter));
        return list;
    }

    [Fact]
    public void Endpoints_And_Anchors_Are_Exact()
    {
        var raw = new List<CursorEvent>
        {
            E(0,   0,   0),
            E(100, 100, 0),
            E(200, 200, 0),
            E(300, 300, 0),
        };
        var smoother = new CursorSmoother(SmoothingIntensity.Medium);
        var out60 = smoother.Smooth(raw, 60);

        Assert.True(out60[0].Position.X <= 1e-6);
        Assert.True(System.Math.Abs(out60[^1].Position.X - 300) <= 1e-6);
    }

    [Fact]
    public void Spline_Interpolates_Between_Anchors()
    {
        // Collinear control points: any centripetal CR must stay on the line.
        var mid = CursorSmoother.CentripetalCatmullRom(
            new(0, 0), new(100, 0), new(200, 0), new(300, 0), 0.5, alpha: 0.5);
        Assert.Equal(150.0, mid.X, 4);
        Assert.Equal(0.0, mid.Y, 4);
    }

    [Fact]
    public void Centripetal_Endpoints_Are_Exact()
    {
        // f(0) must equal p1 and f(1) must equal p2 regardless of alpha.
        var p0 = new Vec2(0, 0); var p1 = new Vec2(100, 50);
        var p2 = new Vec2(200, -30); var p3 = new Vec2(300, 10);
        foreach (var alpha in new[] { 0.0, 0.5, 1.0 })
        {
            Assert.Equal(p1, CursorSmoother.CentripetalCatmullRom(p0, p1, p2, p3, 0.0, alpha));
            Assert.Equal(p2, CursorSmoother.CentripetalCatmullRom(p0, p1, p2, p3, 1.0, alpha));
        }
    }

    [Fact]
    public void Centripetal_Reduces_Overshoot_On_Sharp_Turn()
    {
        // A sharp V-turn: p1 is the apex. Uniform CR (alpha=0) overshoots below the apex;
        // centripetal (alpha=0.5) stays closer to the control polygon.
        var p0 = new Vec2(-100, 0);
        var p1 = new Vec2(0, 0);     // apex
        var p2 = new Vec2(100, 0);   // continuing up
        var p3 = new Vec2(200, 100);

        var uniform = CursorSmoother.CentripetalCatmullRom(p0, p1, p2, p3, 0.5, alpha: 0.0);
        var centripetal = CursorSmoother.CentripetalCatmullRom(p0, p1, p2, p3, 0.5, alpha: 0.5);

        // Overshoot = how far below y=0 the midpoint dips. Centripetal dips less.
        var uniformOvershoot = -uniform.Y;     // positive if it dips below 0
        var centripetalOvershoot = -centripetal.Y;
        Assert.True(centripetalOvershoot <= uniformOvershoot + 1e-9,
            $"centripetal overshoot {centripetalOvershoot} should be <= uniform {uniformOvershoot}");
    }

    [Fact]
    public void Click_Positions_Are_Preserved_As_Anchors()
    {
        // A click sample must not drift; its coordinates appear verbatim in output
        // because the smoother snaps the nearest output frame to it.
        var raw = new List<CursorEvent>
        {
            E(0,   10, 10),
            E(50,  40, 12),
            E(100, 70, 10, CursorButtonState.Left), // click here
            E(150, 100, 12),
            E(200, 130, 10),
        };
        var smoother = new CursorSmoother(SmoothingIntensity.Heavy);
        var out60 = smoother.Smooth(raw, 60);

        var clickFrame = out60.FirstOrDefault(f => f.Buttons == CursorButtonState.Left);
        Assert.True(clickFrame != default, "no click frame emitted");
        Assert.Equal(70.0, clickFrame.Position.X, 6);
        Assert.Equal(10.0, clickFrame.Position.Y, 6);
    }

    [Fact]
    public void Higher_Intensity_Smooths_Jittered_Line()
    {
        // Heavy smoothing resamples more aggressively against a dense jittered stream;
        // because Heavy uses a larger alpha, the spline follows the jittered waypoints
        // less tightly, yielding lower high-frequency variance in the OUTPUT spacing.
        var raw = JitteredLine(120, dtMs: 8.0, jitter: 12.0, seed: 7);

        var light = new CursorSmoother(SmoothingIntensity.Light).Smooth(raw, 60);
        var heavy = new CursorSmoother(SmoothingIntensity.Heavy).Smooth(raw, 60);

        var lightJitter = StepJitter(raw);
        var heavySmooth = StepJitter(heavy);

        // The raw input jitter is larger than the heavy-smoothed output jitter.
        Assert.True(heavySmooth < lightJitter,
            $"heavy output jitter {heavySmooth} should be < input jitter {lightJitter}");
    }

    [Fact]
    public void SixtyFps_Benchmark_Handles_Long_Recording()
    {
        // 10-minute recording at ~60Hz capture = ~36000 raw samples.
        var raw = JitteredLine(36_000, dtMs: 16.6, jitter: 6.0);
        var smoother = new CursorSmoother(SmoothingIntensity.Medium);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var out60 = smoother.Smooth(raw, 60);
        sw.Stop();

        var spanMs = raw[^1].TimestampMs - raw[0].TimestampMs;
        Assert.Equal((int)(spanMs / (1000.0 / 60)) + 1, out60.Count);

        Assert.True(sw.ElapsedMilliseconds < 10_000,
            $"smoothing 10-min clip took {sw.ElapsedMilliseconds}ms (>10s budget)");
    }

    [Fact]
    public void Empty_And_Single_Sample_Are_Safe()
    {
        var s = new CursorSmoother(SmoothingIntensity.Medium);
        Assert.Empty(s.Smooth(Array.Empty<CursorEvent>(), 60));

        var one = s.Smooth(new[] { E(0, 5, 5) }, 60);
        Assert.Single(one);
        Assert.Equal(5.0, one[0].Position.X, 6);
    }

    /// <summary>Mean absolute frame-to-frame Y delta — a direct measure of high-freq jitter.</summary>
    private static double StepJitter(IReadOnlyList<CursorEvent> frames)
    {
        if (frames.Count < 2) return 0;
        double sum = 0;
        for (int i = 1; i < frames.Count; i++)
            sum += System.Math.Abs(frames[i].Position.Y - frames[i - 1].Position.Y);
        return sum / (frames.Count - 1);
    }
}
