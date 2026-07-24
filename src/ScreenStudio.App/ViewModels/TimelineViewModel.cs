using ScreenStudio.App.ViewModels;
using ScreenStudio.Core.Timeline;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.App.ViewModels;

/// <summary>Task #014 — TimelineViewModel. INPC wrapper around
/// <see cref="RecordingTimeline"/> + <see cref="ZoomProgram"/> (ui-prd §8: the engine types
/// are plain; this surfaces observable keyframes/segments + commands for the editor Canvas).
/// Drives the TimelinePanel: drag keyframes, trim, cut, scrub playhead.</summary>
public sealed class TimelineViewModel : ViewModelBase
{
    public RecordingTimeline Timeline { get; }

    private double _playheadMs;
    public double PlayheadMs
    {
        get => _playheadMs;
        set
        {
            Timeline.SetPlayhead(value);
            SetProperty(ref _playheadMs, value);
        }
    }

    public IReadOnlyList<ZoomKeyframe> Keyframes => Timeline.Zoom.Keyframes;
    public IReadOnlyList<Segment> Segments => Timeline.Segments;
    public double DurationMs => Timeline.DurationMs;
    public double KeptDurationMs => Timeline.KeptDurationMs;

    public TimelineViewModel(RecordingTimeline timeline)
    {
        Timeline = timeline;
        _playheadMs = 0;
    }

    /// <summary>Move a keyframe to a new time (drag). Snaps to nearby keyframes.</summary>
    public bool MoveKeyframe(double oldTimeMs, ZoomKeyframe newValue)
    {
        var snapped = Timeline.SnapToKeyframe(newValue.TimeMs);
        newValue = newValue with { TimeMs = snapped };
        var ok = Timeline.MoveKeyframe(oldTimeMs, newValue);
        if (ok) Refresh();
        return ok;
    }

    public bool RemoveKeyframe(double timeMs)
    {
        var ok = Timeline.RemoveKeyframeAt(timeMs);
        if (ok) Refresh();
        return ok;
    }

    public void Trim(double startMs, double endMs)
    {
        Timeline.TrimEdges(startMs, endMs);
        Refresh();
    }

    public void Cut(double fromMs, double toMs)
    {
        Timeline.Cut(fromMs, toMs);
        Refresh();
    }

    /// <summary>Notify observers that the underlying keyframe/segment collections changed
    /// (engine lists aren't observable; the Canvas re-reads on this signal).</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Keyframes));
        OnPropertyChanged(nameof(Segments));
        OnPropertyChanged(nameof(KeptDurationMs));
    }
}
