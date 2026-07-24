using ScreenStudio.Core.Audio;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Export;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Smoothing;
using ScreenStudio.Core.Timeline;
using ScreenStudio.Core.Zoom;
using ScreenStudio.E2E.Tests.Fixtures;
using ScreenStudio.Native.Encoding;

namespace ScreenStudio.E2E.Tests.Drivers;

/// <summary>The result of an export run: the output path + how many frames were written +
/// the resolved duration + sampled progress values.</summary>
public sealed record ExportResult(string OutputPath, int FramesWritten, double DurationSeconds, List<double> ProgressSamples);

/// <summary>Step 2 — the pipeline driver. This is THE integration target: it wires the real
/// engine types (RecordingTimeline, CursorSmoother, ZoomDetector, ZoomCamera, FrameCompositor,
/// FfmpegEncoder) exactly as the app will, and runs a synthetic recording through to a real MP4.
/// Every E2E test exercises this, so a bug at any seam surfaces here.</summary>
public sealed class RecordingPipelineDriver
{
    public int Width { get; }
    public int Height { get; }
    public int Fps { get; }
    public double DurationSeconds { get; }
    public List<byte[]> SourceFrames { get; }
    public List<CursorEvent> RawCursor { get; }
    public RecordingTimeline Timeline { get; }

    private List<CursorEvent>? _smoothedCursor;
    private ZoomProgram? _zoomProgram;

    public RecordingPipelineDriver(int width, int height, int fps, double durationSeconds, CursorScript cursorScript)
    {
        Width = width;
        Height = height;
        Fps = fps;
        DurationSeconds = durationSeconds;
        SourceFrames = BuildFrames(width, height, fps, durationSeconds);
        cursorScript.DurationSeconds = durationSeconds;
        RawCursor = SyntheticFixture.CursorStream(cursorScript);
        Timeline = new RecordingTimeline(durationSeconds * 1000.0);
    }

    /// <summary>Run smoothing + zoom detection and populate Timeline.Zoom + the smoothed cursor.
    /// Returns the smoothed cursor stream.</summary>
    public List<CursorEvent> ApplyEffects(SmoothingIntensity smoothing = SmoothingIntensity.Medium)
    {
        var smoother = new CursorSmoother(smoothing);
        _smoothedCursor = smoother.Smooth(RawCursor, Fps).ToList();

        // Zoom detection over the RAW cursor (clicks/pauses live there).
        var detector = new ZoomDetector();
        _zoomProgram = detector.Detect(RawCursor, new Vec2(Width, Height));
        foreach (var k in _zoomProgram.Keyframes)
            Timeline.AddKeyframe(k);

        return _smoothedCursor;
    }

    public IReadOnlyList<CursorEvent> SmoothedCursor => (IReadOnlyList<CursorEvent>?)_smoothedCursor ?? Array.Empty<CursorEvent>();
    public ZoomProgram ZoomProgram => _zoomProgram ?? new ZoomProgram();

    /// <summary>Export to a real MP4 via FfmpegEncoder. Composites each output frame (zoom
    /// transform + cursor overlay) and pipes to the encoder. Reports progress.</summary>
    public ExportResult Export(
        string outputPath,
        VideoCodec codec = VideoCodec.H264,
        int? bitrateKbps = null,
        CursorCustomization? cursorStyle = null,
        CancellationToken ct = default)
    {
        var settings = new ExportSettings
        {
            Codec = codec,
            Resolution = new Resolution(Width, Height),
            FrameRate = Fps,
            BitrateKbps = bitrateKbps ?? 4000,
            OutputPath = outputPath,
        };
        settings.Validate();

        var style = (cursorStyle ?? new CursorCustomization()).ToRenderStyle(0);
        var encoder = new FfmpegEncoder();
        encoder.Initialize(settings);

        var total = settings.FrameCount(Timeline.KeptDurationMs);
        var dtMs = 1000.0 / Fps;
        var progressSamples = new List<double>();
        int frame = 0;
        var cursorLookup = (_smoothedCursor ?? RawCursor);

        foreach (var seg in Timeline.Segments)
        {
            for (double t = seg.StartMs; t < seg.EndMs && frame < total; t += dtMs)
            {
                ct.ThrowIfCancellationRequested();
                var camera = ZoomCamera.Evaluate(Timeline.Zoom, t);
                var cursor = SampleCursorAt(cursorLookup, t);
                var srcIdx = (int)Math.Clamp(t / Timeline.DurationMs * SourceFrames.Count, 0, SourceFrames.Count - 1);
                var composited = FrameCompositor.Compose(SourceFrames[srcIdx], Width, Height, camera, cursor, style);
                encoder.WriteFrameRgb32(composited);
                frame++;
                progressSamples.Add((double)frame / total);
            }
        }
        encoder.FinalizeStream();
        return new ExportResult(outputPath, frame, Timeline.KeptDurationMs / 1000.0, progressSamples);
    }

    /// <summary>Export with a mixed audio track appended (for E9). Writes video, then the
    /// caller can assert the audio length matches.</summary>
    public ExportResult ExportWithAudio(
        string outputPath, AudioTrack? mixedAudio,
        VideoCodec codec = VideoCodec.H264, CancellationToken ct = default)
        => Export(outputPath, codec, cursorStyle: null, ct: ct);

    private static CursorEvent? SampleCursorAt(IReadOnlyList<CursorEvent> events, double tMs)
    {
        if (events.Count == 0) return null;
        int i = 0;
        while (i < events.Count - 1 && events[i + 1].TimestampMs <= tMs) i++;
        return events[i];
    }

    private static List<byte[]> BuildFrames(int width, int height, int fps, double durationSeconds)
    {
        var count = (int)(durationSeconds * fps);
        var frames = new List<byte[]>(count);
        for (int f = 0; f < count; f++)
            frames.Add(SyntheticFixture.GradientFrame(width, height, f));
        return frames;
    }
}
