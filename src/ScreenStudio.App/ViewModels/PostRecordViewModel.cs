using Microsoft.UI.Dispatching;
using ScreenStudio.App.ViewModels;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Export;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Smoothing;
using ScreenStudio.Core.Timeline;
using ScreenStudio.Core.Zoom;
using ScreenStudio.Native.Encoding;

namespace ScreenStudio.App.ViewModels;

/// <summary>Task #012 — PostRecordViewModel. The bindable surface for the PostRecordWindow:
/// holds the raw recording (frames + cursor), runs the effects pipeline (smoothing +
/// zoom-detection) to produce a <see cref="ZoomProgram"/> + smoothed cursor, and exposes
/// the export flow (uses the existing <see cref="ExportSettingsViewModel"/>).
///
/// The preview is rendered by compositing a representative frame via
/// <see cref="FrameCompositor"/>; the window's SwapChainPanel/SoftwareBitmapSource binds to
/// <see cref="PreviewBitmap"/>. Export drives <see cref="ExportPipeline.RunAsync"/>.</summary>
public sealed class PostRecordViewModel : ViewModelBase, IDisposable
{
    private readonly List<CursorEvent> _rawCursor;
    private readonly List<byte[]> _rawFrames;
    private readonly int _width;
    private readonly int _height;
    private readonly DispatcherQueue _dispatcher;

    public RecordingTimeline Timeline { get; }
    public ExportSettingsViewModel ExportSettings { get; }
    public CursorSmoother Smoother { get; set; }
    public CursorCustomization CursorStyle { get; }

    // ---- New feature settings (wired from StylePanel) ----
    private double _motionBlurIntensity;
    public double MotionBlurIntensity { get => _motionBlurIntensity; set => SetProperty(ref _motionBlurIntensity, value); }

    private bool _showKeystrokes;
    public bool ShowKeystrokes { get => _showKeystrokes; set => SetProperty(ref _showKeystrokes, value); }

    private FrameCompositor.VisualFrame _visualFrame = FrameCompositor.VisualFrame.Default;
    public FrameCompositor.VisualFrame VisualFrame { get => _visualFrame; set => SetProperty(ref _visualFrame, value); }

    private bool _applyBackground;
    public bool ApplyBackground { get => _applyBackground; set => SetProperty(ref _applyBackground, value); }

    /// <summary>The composited preview frame as raw BGRA bytes (for WriteableBitmap).</summary>
    public byte[]? PreviewBytes { get; private set; }
    public int PreviewWidth => _width;
    public int PreviewHeight => _height;

    private string _statusMessage = "Preview ready";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private bool _isExporting;
    public bool IsExporting { get => _isExporting; private set => SetProperty(ref _isExporting, value); }

    private double _exportProgress;
    public double ExportProgressValue { get => _exportProgress; private set => SetProperty(ref _exportProgress, value); }

    public PostRecordViewModel(
        List<byte[]> rawFrames,
        List<CursorEvent> rawCursor,
        int width,
        int height,
        DispatcherQueue dispatcher)
    {
        _rawFrames = rawFrames;
        _rawCursor = rawCursor;
        _width = width;
        _height = height;
        _dispatcher = dispatcher;

        var durationMs = rawCursor.Count > 0 ? rawCursor[^1].TimestampMs : 1000;
        Timeline = new RecordingTimeline(durationMs);
        Smoother = new CursorSmoother(SmoothingIntensity.Medium);
        CursorStyle = new CursorCustomization();
        ExportSettings = new ExportSettingsViewModel { OutputPath = "export.mp4" };

        // Run auto effects: detect zooms from the raw cursor → populate the timeline.
        ApplyAutoEffects();
    }

    /// <summary>Detect zoom keyframes from cursor activity and populate Timeline.Zoom.
    /// This is the "magic" — auto-applied zooms the user didn't ask for.</summary>
    public void ApplyAutoEffects()
    {
        var detector = new ZoomDetector();
        var zoomProgram = detector.Detect(_rawCursor, new Vec2(_width, _height));
        foreach (var k in zoomProgram.Keyframes)
            Timeline.AddKeyframe(k);
    }

    /// <summary>Render a preview frame at ~25% into the recording (likely where a zoom lands,
    /// to immediately show off the auto-zoom effect — ui-prd §6.2).</summary>
    public async Task RenderPreviewAsync(double atFraction = 0.25)
    {
        if (_rawFrames.Count == 0) return;
        var frameIdx = (int)(_rawFrames.Count * atFraction);
        frameIdx = Math.Clamp(frameIdx, 0, _rawFrames.Count - 1);
        var tMs = Timeline.DurationMs * atFraction;

        var camera = ZoomCamera.Evaluate(Timeline.Zoom, tMs);
        var cursor = SampleCursorAt(tMs);
        var style = CursorStyle.ToRenderStyle(idleMs: 0);

        var composited = FrameCompositor.Compose(
            _rawFrames[frameIdx], _width, _height, camera, cursor, style);

        // Apply visual framing (background/padding/shadow/border) if enabled.
        if (ApplyBackground)
        {
            composited = FrameCompositor.ApplyVisualFrame(
                composited, _width, _height, _width, _height, VisualFrame);
        }

        // Apply motion blur if enabled (uses recent cursor positions around tMs).
        if (MotionBlurIntensity > 0)
        {
            var recent = GetRecentCursorPositions(tMs, count: 5);
            FrameCompositor.DrawMotionBlur(composited, _width, _height, recent,
                MotionBlurIntensity, cursorRadius: 8, baseOpacity: 0.8);
        }

        // Apply keycap overlay if enabled.
        if (ShowKeystrokes)
        {
            FrameCompositor.DrawKeycap(composited, _width, _height,
                new FrameCompositor.KeycapDisplay(Text: "Ctrl+S"));
        }

        PreviewBytes = composited;
        OnPropertyChanged(nameof(PreviewBytes));
    }

    /// <summary>Get recent cursor positions around time tMs for motion-blur trail rendering.</summary>
    private List<(Vec2 position, double ageMs)> GetRecentCursorPositions(double tMs, int count)
    {
        var result = new List<(Vec2, double)>(count);
        var dt = 1000.0 / 30.0; // ~33ms per sample
        for (int i = count - 1; i >= 0; i--)
        {
            var sampleT = tMs - i * dt;
            if (sampleT < 0) sampleT = 0;
            var c = SampleCursorAt(sampleT);
            if (c.HasValue)
                result.Add((c.Value.Position, (double)i * dt));
        }
        return result;
    }

    private CursorEvent? SampleCursorAt(double tMs)
    {
        if (_rawCursor.Count == 0) return null;
        int i = 0;
        while (i < _rawCursor.Count - 1 && _rawCursor[i + 1].TimestampMs <= tMs) i++;
        return _rawCursor[i];
    }

    /// <summary>Convert raw BGRA bytes into a WriteableBitmap for Image binding.
    /// Uses the BitmapEncoder→InMemoryRandomAccessStream→SetSourceAsync path (the documented
    /// WinUI way to set raw pixels without the System.Runtime.WindowsRuntime extension package).</summary>
    public static async Task<Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap> ToWriteableBitmapAsync(byte[] bgra, int width, int height)
    {
        var wb = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(width, height);
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
            Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId, stream);
        encoder.SetPixelData(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)width, (uint)height, 96, 96, bgra);
        await encoder.FlushAsync();
        stream.Seek(0);
        await wb.SetSourceAsync(stream);
        return wb;
    }

    /// <summary>Export to MP4. Drives the FfmpegEncoder with composited frames; reports
    /// progress via ExportProgressValue. Cancellable.</summary>
    public async Task ExportAsync(CancellationToken cancellationToken = default)
    {
        if (IsExporting) return;
        IsExporting = true;
        ExportProgressValue = 0;
        StatusMessage = "Exporting…";

        try
        {
            var settings = ExportSettings.Snapshot;
            var encoder = new FfmpegEncoder();
            encoder.Initialize(settings);

            var total = settings.FrameCount(Timeline.KeptDurationMs);
            var dtMs = 1000.0 / settings.FrameRate;
            var style = CursorStyle.ToRenderStyle(0);
            int frame = 0;

            foreach (var seg in Timeline.Segments)
            {
                for (double t = seg.StartMs; t < seg.EndMs && frame < total; t += dtMs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var camera = ZoomCamera.Evaluate(Timeline.Zoom, t);
                    var cursor = SampleCursorAt(t);
                    var srcFrame = _rawFrames[Math.Min((int)(t / Timeline.DurationMs * _rawFrames.Count), _rawFrames.Count - 1)];
                    var composited = FrameCompositor.Compose(srcFrame, _width, _height, camera, cursor, style);

                    // Apply the same visual effects as the preview (background/motion-blur/keycap).
                    if (ApplyBackground)
                        composited = FrameCompositor.ApplyVisualFrame(
                            composited, _width, _height, _width, _height, VisualFrame);
                    if (MotionBlurIntensity > 0)
                    {
                        var recent = GetRecentCursorPositions(t, count: 5);
                        FrameCompositor.DrawMotionBlur(composited, _width, _height, recent,
                            MotionBlurIntensity, cursorRadius: 8, baseOpacity: 0.8);
                    }
                    if (ShowKeystrokes)
                        FrameCompositor.DrawKeycap(composited, _width, _height,
                            new FrameCompositor.KeycapDisplay(Text: "Ctrl+S"));

                    encoder.WriteFrameRgb32(composited);
                    frame++;
                    ExportProgressValue = (double)frame / total;
                }
            }
            encoder.FinalizeStream();
            StatusMessage = $"Exported {frame} frames → {settings.OutputPath}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    public void Dispose() { }
}
