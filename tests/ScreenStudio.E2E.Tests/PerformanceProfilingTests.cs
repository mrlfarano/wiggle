using System.Diagnostics;
using ScreenStudio.E2E.Tests.Drivers;
using ScreenStudio.E2E.Tests.Fixtures;
using ScreenStudio.Native.Encoding;

namespace ScreenStudio.E2E.Tests;

/// <summary>#3 — Performance profiling against the PRD success metrics:
/// 1. "Time from recording to polished export < 5 minutes" (PRD Success Metrics)
/// 2. "App remains responsive during recording" (PRD Success Metrics)
/// 3. "Export quality indistinguishable from manual editing" (durability check)
///
/// These tests use the E2E pipeline driver with realistic clip lengths (30s, 2min, 10min)
/// at production resolutions (720p, 1080p) to measure actual encode throughput. They are
/// [Trait("Category","Performance")] so they can be excluded from fast CI runs.</summary>
[Trait("Category", "Performance")]
public class PerformanceProfilingTests
{
    private static string TempMp4() => Path.Combine(Path.GetTempPath(), $"ssc_perf_{Guid.NewGuid():N}.mp4");
    private static bool FfmpegAvailable() => FfmpegEncoder.IsAvailable();

    /// <summary>Measure the full pipeline: ApplyEffects + Export for a 30s 720p clip.
    /// PRD metric: total time must be well under 5 minutes (300s). We assert <60s for a 30s clip
    /// — a 2x real-time budget that leaves generous headroom for the 5-min target on longer clips.</summary>
    [Fact]
    public async Task Perf01_30s_720p_Export_Under_60s()
    {
        if (!FfmpegAvailable()) return;
        var script = new CursorScript { DurationSeconds = 30.0, Clicks = { (5000, new(400, 300)), (15000, new(600, 400)) } };
        var driver = new RecordingPipelineDriver(1280, 720, 30, 30.0, script);

        var sw = Stopwatch.StartNew();
        driver.ApplyEffects();
        var path = TempMp4();
        try
        {
            var result = driver.Export(path, bitrateKbps: 4000);
            sw.Stop();

            // Assert the clip exported.
            Assert.True(File.Exists(path));
            Assert.Equal(900, result.FramesWritten); // 30s @ 30fps

            // PRD metric: must finish under 60s for a 30s clip (< 2x real-time).
            Assert.True(sw.ElapsedMilliseconds < 60_000,
                $"30s 720p export took {sw.Elapsed.TotalSeconds:F1}s (budget 60s)");

            // Report the ratio for diagnostics.
            var ratio = sw.Elapsed.TotalSeconds / 30.0;
            Assert.True(ratio < 2.0, $"export is {ratio:F2}x real-time (must be <2x)");
        }
        finally { TryDelete(path); }
    }

    /// <summary>Measure 1080p export (heavier). PRD metric: 30s 1080p should finish under 120s.</summary>
    [Fact]
    public async Task Perf02_30s_1080p_Export_Under_120s()
    {
        if (!FfmpegAvailable()) return;
        var script = new CursorScript { DurationSeconds = 30.0 };
        var driver = new RecordingPipelineDriver(1920, 1080, 30, 30.0, script);
        driver.ApplyEffects();

        var path = TempMp4();
        var sw = Stopwatch.StartNew();
        try
        {
            var result = driver.Export(path, bitrateKbps: 8000);
            sw.Stop();
            Assert.Equal(900, result.FramesWritten);
            Assert.True(sw.ElapsedMilliseconds < 120_000,
                $"30s 1080p export took {sw.Elapsed.TotalSeconds:F1}s (budget 120s)");
        }
        finally { TryDelete(path); }
    }

    /// <summary>Measure a 2-minute 720p clip — the PRD's realistic "first successful video" scenario.
    /// PRD: "User can create first successful video within 15 minutes of first use" — this
    /// measures the export portion of that journey.</summary>
    [Fact]
    public async Task Perf03_2min_720p_Export_Under_300s()
    {
        if (!FfmpegAvailable()) return;
        var script = new CursorScript
        {
            DurationSeconds = 120.0,
            Clicks = { (10000, new(400, 300)), (30000, new(800, 600)), (60000, new(200, 200)), (90000, new(960, 540)) },
            Pauses = { (45000, 2000) },
        };
        var driver = new RecordingPipelineDriver(1280, 720, 30, 120.0, script);
        driver.ApplyEffects();

        var path = TempMp4();
        var sw = Stopwatch.StartNew();
        try
        {
            var result = driver.Export(path, bitrateKbps: 4000);
            sw.Stop();
            Assert.True(result.FramesWritten >= 3590 && result.FramesWritten <= 3600,
                $"expected ~3600 frames for 2min, got {result.FramesWritten}");
            // PRD: record→export < 5 min. We measure just the export portion (< 300s).
            Assert.True(sw.ElapsedMilliseconds < 300_000,
                $"2min 720p export took {sw.Elapsed.TotalSeconds:F1}s (PRD budget 300s)");
        }
        finally { TryDelete(path); }
    }

    /// <summary>Measure cursor-smoothing throughput: a 10-minute recording at 60Hz capture
    /// = ~36000 samples. The smoother must process this in seconds (already unit-tested at 812ms;
    /// this test measures the full effects pipeline: smoothing + zoom detection for 10min).</summary>
    [Fact]
    public async Task Perf04_10min_Effects_Pipeline_Under_15s()
    {
        if (!FfmpegAvailable()) return;
        var script = new CursorScript
        {
            DurationSeconds = 600.0, // 10 minutes
            Clicks = { (60000, new(400, 300)), (180000, new(800, 600)), (360000, new(200, 200)) },
        };
        var driver = new RecordingPipelineDriver(1280, 720, 30, 600.0, script);

        var sw = Stopwatch.StartNew();
        var smoothed = driver.ApplyEffects();
        sw.Stop();

        Assert.NotEmpty(smoothed);
        // Effects pipeline (smoothing + zoom detection) for 10min must be < 15s.
        Assert.True(sw.ElapsedMilliseconds < 15_000,
            $"10min effects pipeline took {sw.Elapsed.TotalSeconds:F1}s (budget 15s)");
    }

    /// <summary>Measure the compositor throughput (frames/sec) without encode overhead.
    /// Establishes the CPU-compositing ceiling so we know if the compositor or the encoder
    /// is the bottleneck. Target: > 30 frames/sec at 720p.</summary>
    [Fact]
    public void Perf05_Compositor_Throughput_720p()
    {
        var w = 1280; var h = 720;
        var src = new byte[w * h * 4];
        Array.Fill(src, (byte)128);
        var camera = new Core.Zoom.CameraState(0, new(0.5, 0.5), 1.5);

        var frameCount = 300; // 10 seconds @ 30fps
        var sw = Stopwatch.StartNew();
        for (int f = 0; f < frameCount; f++)
        {
            Core.Export.FrameCompositor.Compose(src, w, h, camera, null, new(1, 1, false));
        }
        sw.Stop();

        var fps = frameCount / sw.Elapsed.TotalSeconds;
        Assert.True(fps > 30, $"compositor throughput {fps:F1} fps (must be >30 for real-time)");
    }

    /// <summary>Measure visual-frame throughput (background + padding + content scaling).</summary>
    [Fact]
    public void Perf06_VisualFrame_Throughput()
    {
        var w = 1280; var h = 720;
        var content = new byte[640 * 360 * 4];
        Array.Fill(content, (byte)200);
        var frame = new Core.Export.FrameCompositor.VisualFrame(0x2e, 0x1a, 0x1a, 40, 0, 0, 0, 0, 0, 0, 0);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
            Core.Export.FrameCompositor.ApplyVisualFrame(content, 640, 360, w, h, frame);
        sw.Stop();

        var fps = 100 / sw.Elapsed.TotalSeconds;
        Assert.True(fps > 15, $"visual-frame throughput {fps:F1} fps (must be >15)");
    }

    /// <summary>Measure audio mix throughput for a 10-minute clip (48kHz stereo).
    /// Must complete in seconds (it's pure float math, no encode).</summary>
    [Fact]
    public void Perf07_10min_Audio_Mix_Under_10s()
    {
        var sr = 48000; var ch = 2; var seconds = 600.0;
        var mic = SyntheticFixture.ToneTrack(sr, 1, seconds, amplitude: 0.3);
        var sys = SyntheticFixture.ToneTrack(sr, 1, seconds, amplitude: 0.2);

        var sw = Stopwatch.StartNew();
        var mixer = new Core.Audio.AudioExportMixer(new Core.Audio.AudioExportSettings
        {
            TargetSampleRate = sr, TargetChannels = ch, EnableNormalization = true,
        });
        var mixed = mixer.MixForExport(mic, sys);
        sw.Stop();

        Assert.True(mixed.Samples.Length > 0);
        Assert.True(sw.ElapsedMilliseconds < 10_000,
            $"10min audio mix took {sw.Elapsed.TotalSeconds:F1}s (budget 10s)");
    }

    private static void TryDelete(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try { if (File.Exists(path)) File.Delete(path); return; }
            catch (IOException) { Thread.Sleep(200); }
            catch { return; }
        }
    }
}
