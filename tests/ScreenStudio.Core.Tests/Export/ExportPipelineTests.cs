using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Export;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Smoothing;
using ScreenStudio.Core.Timeline;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.Core.Tests.Export;

public class ExportPipelineTests
{
    private static CursorEvent E(double t, double x, double y) => new(t, new(x, y), CursorButtonState.None);

    private static List<CursorEvent> Line(int n)
    {
        var l = new List<CursorEvent>(n);
        for (int i = 0; i < n; i++) l.Add(E(i * 16.6, i * 5, 100));
        return l;
    }

    [Fact]
    public void Settings_Reject_Invalid_FrameRate()
    {
        var s = new ExportSettings { FrameRate = 45 };
        Assert.Throws<InvalidOperationException>(() => s.Validate());
    }

    [Fact]
    public void Settings_Accept_30_And_60()
    {
        new ExportSettings { FrameRate = 30 }.Validate();
        new ExportSettings { FrameRate = 60 }.Validate();
    }

    [Fact]
    public async Task Run_Produces_One_Frame_Per_Output_Frame_And_Progress()
    {
        var tl = new RecordingTimeline(1000); // 1s
        var raw = Line(80);
        var sink = new List<ComposeFrame>();
        var pipe = new ExportPipeline((f, ct) => { sink.Add(f); return Task.CompletedTask; });

        var settings = new ExportSettings { FrameRate = 60, Resolution = Resolution.HD1080p };
        var style = new CursorRenderStyle(1.0, 1.0, true);

        var progresses = new List<ExportProgress>();
        await foreach (var p in pipe.RunAsync(tl, raw, new CursorSmoother(), style, settings, new(1920, 1080)))
            progresses.Add(p);

        // 1s @ 60fps = 60 frames.
        Assert.Equal(60, sink.Count);
        Assert.Equal(60, progresses.Count);
        Assert.Equal(1.0, progresses[^1].Fraction, 6);
        Assert.True(progresses[0].Fraction < progresses[^1].Fraction);
    }

    [Fact]
    public async Task Run_Honors_Cuts_Only_Composes_Kept_Time()
    {
        var tl = new RecordingTimeline(2000);
        tl.Cut(1000, 2000); // keep only first second
        var raw = Line(160);
        var sink = new List<ComposeFrame>();
        var pipe = new ExportPipeline((f, ct) => { sink.Add(f); return Task.CompletedTask; });

        var settings = new ExportSettings { FrameRate = 60 };
        var style = new CursorRenderStyle(1.0, 1.0, true);

        await foreach (var _ in pipe.RunAsync(tl, raw, new CursorSmoother(), style, settings, new(1920, 1080))) { }

        // Kept = 1s -> 60 frames despite the 2s recording.
        Assert.Equal(60, sink.Count);
        // No frame should be past the cut boundary.
        Assert.All(sink, f => Assert.InRange(f.TimeMs, 0, 1000));
    }

    [Fact]
    public async Task Run_Cancels_Mid_Stream()
    {
        var tl = new RecordingTimeline(100_000);
        var raw = Line(6000);
        var sink = new List<ComposeFrame>();
        var pipe = new ExportPipeline((f, ct) => { sink.Add(f); return Task.CompletedTask; });

        using var cts = new CancellationTokenSource();
        var settings = new ExportSettings { FrameRate = 60 };
        var style = new CursorRenderStyle(1.0, 1.0, true);

        int n = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in pipe.RunAsync(tl, raw, new CursorSmoother(), style, settings, new(1920, 1080), cts.Token))
            {
                if (++n == 10) cts.Cancel();
            }
        });
        Assert.True(sink.Count < 6000, "export should have stopped early on cancel");
    }
}
