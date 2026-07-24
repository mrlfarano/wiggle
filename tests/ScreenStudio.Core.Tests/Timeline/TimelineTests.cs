using ScreenStudio.Core.Math;
using ScreenStudio.Core.Timeline;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.Core.Tests.Timeline;

public class TimelineTests
{
    [Fact]
    public void Defaults_To_One_Full_Segment()
    {
        var tl = new RecordingTimeline(10_000);
        Assert.Equal(10_000, tl.DurationMs);
        Assert.Single(tl.Segments);
        Assert.Equal(10_000, tl.KeptDurationMs);
    }

    [Fact]
    public void Trim_Reduces_Kept_Duration()
    {
        var tl = new RecordingTimeline(10_000);
        tl.TrimEdges(2000, 8000);
        Assert.Equal(6000, tl.KeptDurationMs);
        Assert.Equal(2000, tl.Segments[0].StartMs);
        Assert.Equal(8000, tl.Segments[0].EndMs);
    }

    [Fact]
    public void Cut_Splits_Segment()
    {
        var tl = new RecordingTimeline(10_000);
        tl.Cut(4000, 6000); // remove the middle
        Assert.Equal(2, tl.Segments.Count);
        Assert.Equal(0, tl.Segments[0].StartMs);
        Assert.Equal(4000, tl.Segments[0].EndMs);
        Assert.Equal(6000, tl.Segments[1].StartMs);
        Assert.Equal(10_000, tl.Segments[1].EndMs);
        Assert.Equal(8000, tl.KeptDurationMs); // 4s + 4s
    }

    [Fact]
    public void Cut_Disjoint_Leaves_Segment_Intact()
    {
        var tl = new RecordingTimeline(10_000);
        tl.Cut(20_000, 30_000); // outside range -> ignored
        Assert.Single(tl.Segments);
    }

    [Fact]
    public void Keyframe_Add_Move_Remove()
    {
        var tl = new RecordingTimeline(10_000);
        tl.AddKeyframe(new ZoomKeyframe(1000, new(0.5, 0.5), 2.0));
        Assert.Equal(1, tl.Zoom.Count);

        Assert.True(tl.MoveKeyframe(1000, new ZoomKeyframe(1500, new(0.6, 0.6), 2.5)));
        Assert.Equal(1500, tl.Zoom.Keyframes[0].TimeMs);

        Assert.True(tl.RemoveKeyframeAt(1500));
        Assert.Equal(0, tl.Zoom.Count);
        Assert.False(tl.RemoveKeyframeAt(9999));
    }

    [Fact]
    public void Playhead_Clamps_And_Snaps()
    {
        var tl = new RecordingTimeline(10_000);
        tl.AddKeyframe(new ZoomKeyframe(2000, new(0.5, 0.5), 2.0));

        tl.SetPlayhead(99_999);
        Assert.Equal(10_000, tl.PlayheadMs);

        tl.SetPlayhead(-5);
        Assert.Equal(0, tl.PlayheadMs);

        // Snap near the keyframe.
        var snapped = tl.SnapToKeyframe(1980, toleranceMs: 100);
        Assert.Equal(2000, snapped);
        // Far from any keyframe => unchanged.
        Assert.Equal(8000, tl.SnapToKeyframe(8000, toleranceMs: 100));
    }

    [Fact]
    public void IsKept_Respects_Cuts()
    {
        var tl = new RecordingTimeline(10_000);
        tl.Cut(4000, 6000);
        Assert.True(tl.IsKept(1000));
        Assert.False(tl.IsKept(5000));
        Assert.True(tl.IsKept(7000));
    }
}
