using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.Core.Tests.Zoom;

public class ZoomDetectorTests
{
    private static CursorEvent E(double t, double x, double y, CursorButtonState b = CursorButtonState.None)
        => new(t, new(x, y), b);

    private static readonly Vec2 Size = new(1920, 1080);

    [Fact]
    public void Empty_Stream_Returns_Empty_Program_After_Seed()
    {
        var det = new ZoomDetector();
        var prog = det.Detect(new List<CursorEvent>(), Size);
        Assert.Empty(prog.Keyframes);
    }

    [Fact]
    public void Click_Triggers_Zoom_In_At_Click_Location()
    {
        var raw = new List<CursorEvent>
        {
            E(0,    100, 100),
            E(100,  200, 200),
            E(200,  960, 540, CursorButtonState.Left), // center click
            E(300,  1000, 600),
            E(1000, 1100, 700),
        };
        var prog = new ZoomDetector().Detect(raw, Size);

        // There must be a keyframe at the click time zoomed in at center.
        var clickKey = prog.Keyframes.FirstOrDefault(k => SysApprox(k.TimeMs, 200));
        Assert.True(clickKey.Scale > 1.0, "click keyframe should zoom in");
        Assert.Equal(0.5, clickKey.RelativeFocus.X, 3);
        Assert.Equal(0.5, clickKey.RelativeFocus.Y, 3);
    }

    [Fact]
    public void Pause_Triggers_Zoom()
    {
        // Cursor stops moving for a long stretch -> pause-detected zoom.
        var raw = new List<CursorEvent>
        {
            E(0,    100, 100),
            E(50,   120, 110),
            E(500,  121, 111),   // ~400ms near-stop
            E(1000, 200, 200),
        };
        var prog = new ZoomDetector().Detect(raw, Size);
        Assert.True(prog.Count > 1, "pause should generate a zoom keyframe");
    }

    [Fact]
    public void No_Activity_Yields_Only_Seed()
    {
        var raw = new List<CursorEvent>
        {
            E(0,    100, 100),
            E(16,   105, 102),
            E(32,   110, 104),
        };
        var prog = new ZoomDetector().Detect(raw, Size);
        Assert.Equal(1, prog.Count); // only the seed
        Assert.Equal(1.0, prog.Keyframes[0].Scale);
    }

    [Fact]
    public void MinGap_Prevents_Keyframe_Spam()
    {
        // Several rapid clicks within the min-gap window should not each spawn keyframes.
        // Final samples move continuously (no pause) so only the click burst is zoomed.
        var raw = new List<CursorEvent>();
        raw.Add(E(0, 100, 100));
        raw.Add(E(200, 200, 200, CursorButtonState.Left));
        raw.Add(E(210, 210, 210, CursorButtonState.Left)); // within MinKeyframeGapMs (250)
        raw.Add(E(220, 220, 220, CursorButtonState.Left));
        // Continuous motion afterwards — NOT a pause.
        for (int i = 1; i <= 20; i++)
            raw.Add(E(300 + i * 50, 250 + i * 30, 250 + i * 20));

        var det = new ZoomDetector { Options = { MinKeyframeGapMs = 250, PauseMinMs = 1000 } };
        var prog = det.Detect(raw, Size);
        // Seed + one zoom-in/hold/out triple for the burst = 4 keyframes total.
        Assert.True(prog.Count == 4, $"expected exactly 4 keys, got {prog.Count}");
    }

    private static bool SysApprox(double a, double b, double eps = 1e-3) => System.Math.Abs(a - b) <= eps;
}

public class ZoomCameraTests
{
    [Fact]
    public void EaseInOutCubic_Endpoints_And_Monotone()
    {
        Assert.Equal(0.0, ZoomCamera.EaseInOutCubic(0.0), 6);
        Assert.Equal(1.0, ZoomCamera.EaseInOutCubic(1.0), 6);
        double prev = 0;
        for (double t = 0; t <= 1; t += 0.1)
        {
            var v = ZoomCamera.EaseInOutCubic(t);
            Assert.True(v >= prev - 1e-9, "ease should be monotonic non-decreasing");
            prev = v;
        }
    }

    [Fact]
    public void Evaluate_Interpolates_Between_Keyframes()
    {
        var prog = new ZoomProgram()
            .Add(new ZoomKeyframe(0,   new(0.5, 0.5), 1.0))
            .Add(new ZoomKeyframe(1000, new(0.5, 0.5), 3.0));
        var mid = ZoomCamera.Evaluate(prog, 500);
        // At t=0.5 with cubic ease, value is exactly 0.5.
        Assert.Equal(2.0, mid.Scale, 4); // 1 + (3-1)*0.5
    }

    [Fact]
    public void Evaluate_Clamps_Outside_Range()
    {
        var prog = new ZoomProgram()
            .Add(new ZoomKeyframe(100, new(0.2, 0.2), 2.0))
            .Add(new ZoomKeyframe(200, new(0.8, 0.8), 1.0));
        Assert.Equal(2.0, ZoomCamera.Evaluate(prog, 0).Scale, 6);   // before -> first
        Assert.Equal(1.0, ZoomCamera.Evaluate(prog, 500).Scale, 6); // after -> last
    }
}
