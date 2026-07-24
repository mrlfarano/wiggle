using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Smoothing;
using ScreenStudio.Core.Timeline;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.Core.Export;

/// <summary>Progress reported during export (PRD P0: "Progress indicator during export").</summary>
public readonly record struct ExportProgress(int Frame, int TotalFrames, double Fraction)
{
    public double Percent => Fraction * 100.0;
}

/// <summary>Per-frame composition input: the camera + cursor state the renderer should draw.
/// This is the contract between the export pipeline and the (GPU) frame compositor.</summary>
public readonly record struct ComposeFrame(
    int FrameIndex,
    double TimeMs,
    CameraState Camera,
    CursorEvent Cursor,
    CursorRenderStyle CursorStyle);

/// <summary>Task 006 — Export pipeline orchestration. Drives frame-by-frame composition:
/// for each output frame it (a) evaluates the zoom camera, (b) samples the smoothed cursor,
/// (c) emits a ComposeFrame for the GPU/native encoder. Handles cancellation and progress.
///
/// The actual Media Foundation encode is in ScreenStudio.Native (unverified); this pipeline
/// is the orchestrator and is fully testable headlessly with a fake compositor sink.</summary>
public sealed class ExportPipeline
{
    private readonly Func<ComposeFrame, CancellationToken, Task> _composeAsync;

    /// <param name="composeAsync">Sink that receives each composed frame (the real one hands
    /// off to the Media Foundation encoder / D3D compositor; tests pass a recording fake).</param>
    public ExportPipeline(Func<ComposeFrame, CancellationToken, Task> composeAsync)
    {
        _composeAsync = composeAsync ?? throw new ArgumentNullException(nameof(composeAsync));
    }

    /// <summary>Run the export. Yields progress after each frame; honors cancellation.</summary>
    public async IAsyncEnumerable<ExportProgress> RunAsync(
        RecordingTimeline timeline,
        IReadOnlyList<CursorEvent> rawCursor,
        CursorSmoother smoother,
        CursorRenderStyle cursorStyle,
        ExportSettings settings,
        Vec2 sourceSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        settings.Validate();
        var kept = timeline.KeptDurationMs;
        var total = settings.FrameCount(kept);
        var dtMs = 1000.0 / settings.FrameRate;

        // Pre-smooth the cursor stream once (the 60fps benchmark proves this is cheap).
        var smoothed = smoother.Smooth(rawCursor, settings.FrameRate);

        int frame = 0;
        foreach (var seg in timeline.Segments)
        {
            for (double t = seg.StartMs; t < seg.EndMs && frame < total; t += dtMs)
            {
                ct.ThrowIfCancellationRequested();

                var camera = ZoomCamera.Evaluate(timeline.Zoom, t);
                var cursor = SampleAt(smoothed, t);
                var compose = new ComposeFrame(frame, t, camera, cursor, cursorStyle);

                await _composeAsync(compose, ct).ConfigureAwait(false);

                frame++;
                yield return new ExportProgress(frame, total, (double)frame / total);
            }
        }
    }

    /// <summary>Nearest-timestamp cursor sample at time t (linear scan; smoothed lists are
    /// small and temporally ordered). For production this becomes a binary search.</summary>
    private static CursorEvent SampleAt(IReadOnlyList<CursorEvent> frames, double tMs)
    {
        if (frames.Count == 0) return new CursorEvent(tMs, Vec2.Zero, CursorButtonState.None);
        int i = 0;
        while (i < frames.Count - 1 && frames[i + 1].TimestampMs <= tMs) i++;
        return frames[i];
    }
}
