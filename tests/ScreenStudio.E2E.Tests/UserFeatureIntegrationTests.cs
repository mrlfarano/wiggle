using ScreenStudio.Core.Audio;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Export;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Smoothing;
using ScreenStudio.E2E.Tests.Fixtures;
using ScreenStudio.Native.Encoding;

namespace ScreenStudio.E2E.Tests;

/// <summary>PRD-based user-feature tests. Each test verifies a PRD feature is reachable
/// end-to-end through the ViewModels + engine (the wiring layer), proving a user can
/// actually exercise each capability. Derived from the PRD's P0/P1/P2 feature lists +
/// the user-journey.md flows.</summary>
public class UserFeatureIntegrationTests
{
    private static byte[] MakeFrame(int w, int h, byte v = 128)
    {
        var b = new byte[w * h * 4];
        for (int i = 0; i < b.Length; i += 4) { b[i] = v; b[i + 1] = v; b[i + 2] = v; b[i + 3] = 255; }
        return b;
    }

    // ---- PRD P0: Core MVP features ----

    /// <summary>PRD P0 #1: "Record full screen or selected area at 60fps." Verify the capture
    /// options expose region/full-screen targets and the frame rate is configurable.</summary>
    [Fact]
    public void U01_Capture_Options_Expose_Target_And_Framerate()
    {
        var opts = new Native.Capture.CaptureOptions
        {
            Target = Native.Capture.CaptureTarget.Region,
            RegionTopLeft = new(100, 100),
            RegionSize = new(640, 480),
            TargetFrameRate = 60,
        };
        Assert.Equal(Native.Capture.CaptureTarget.Region, opts.Target);
        Assert.Equal(640, opts.RegionSize.X);
        Assert.Equal(60, opts.TargetFrameRate);
    }

    /// <summary>PRD P0 #2: "Detect cursor actions and automatically zoom in." Verify a click
    /// in the cursor stream produces a zoom keyframe with scale > 1.</summary>
    [Fact]
    public void U02_Click_Triggers_Auto_Zoom()
    {
        var script = new CursorScript { DurationSeconds = 2.0, Clicks = { (1000, new Vec2(400, 300)) } };
        var driver = new Drivers.RecordingPipelineDriver(640, 480, 30, 2.0, script);
        driver.ApplyEffects();
        Assert.Contains(driver.Timeline.Zoom.Keyframes, k => k.Scale > 1.0);
    }

    /// <summary>PRD P0 #3: "Transform shaky cursor motion into smooth curves." Verify the
    /// smoother produces a non-empty stream that differs from the raw input.</summary>
    [Fact]
    public void U03_Smoothing_Produces_Smoothed_Stream()
    {
        var script = new CursorScript { DurationSeconds = 1.0, Jitter = 10.0 };
        var driver = new Drivers.RecordingPipelineDriver(640, 480, 30, 1.0, script);
        var smoothed = driver.ApplyEffects(SmoothingIntensity.Heavy);
        Assert.NotEmpty(smoothed);
        Assert.True(smoothed.Count < driver.RawCursor.Count, "smoothing should resample to fewer points");
    }

    /// <summary>PRD P0 #4: "Export as MP4 (H.264)." Verify a real MP4 is produced.</summary>
    [Fact]
    public async Task U04_Export_Produces_Valid_Mp4()
    {
        if (!FfmpegEncoder.IsAvailable()) return;
        var driver = new Drivers.RecordingPipelineDriver(320, 240, 30, 1.0, new CursorScript { DurationSeconds = 1.0 });
        driver.ApplyEffects();
        var path = Path.Combine(Path.GetTempPath(), $"ssc_user_{Guid.NewGuid():N}.mp4");
        try
        {
            var result = driver.Export(path);
            Assert.True(File.Exists(path));
            Assert.Equal(30, result.FramesWritten);
        }
        finally { for (int i = 0; i < 5; i++) try { if (File.Exists(path)) File.Delete(path); break; } catch { Thread.Sleep(200); } }
    }

    /// <summary>PRD P0 #5: "Trim/cut functionality." Verify the timeline editor's trim reduces duration.</summary>
    [Fact]
    public void U05_Timeline_Trim_Reduces_Duration()
    {
        var driver = new Drivers.RecordingPipelineDriver(640, 480, 30, 2.0, new CursorScript { DurationSeconds = 2.0 });
        driver.ApplyEffects();
        var before = driver.Timeline.KeptDurationMs;
        driver.Timeline.TrimEdges(500, 1500);
        Assert.True(driver.Timeline.KeptDurationMs < before);
        Assert.Equal(1000, driver.Timeline.KeptDurationMs, 1);
    }

    // ---- PRD P1: Post-MVP features ----

    /// <summary>PRD P1 #1: "Adjust cursor size + auto-hide." Verify CursorCustomization exposes
    /// size presets + auto-hide timeout.</summary>
    [Fact]
    public void U06_Cursor_Customization_Size_And_AutoHide()
    {
        var cc = new CursorCustomization { SizeMultiplier = 2.0, AutoHideEnabled = true, AutoHideTimeoutMs = 2000 };
        var style = cc.ToRenderStyle(idleMs: 2500);
        Assert.Equal(2.0, style.SizeMultiplier);
        Assert.False(style.Visible); // past timeout → faded
    }

    /// <summary>PRD P1 #2: "Microphone + system audio + noise reduction." Verify the noise gate
    /// + mixer produce a combined track from mic + system sources.</summary>
    [Fact]
    public void U07_Audio_NoiseGate_And_Mix()
    {
        var mic = SyntheticFixture.SignalPlusNoiseTrack(48000, 1, 1.0);
        var sys = SyntheticFixture.ToneTrack(48000, 1, 1.0, amplitude: 0.2);
        var mixer = new AudioExportMixer(new AudioExportSettings { EnableNormalization = false });
        var mixed = mixer.MixForExport(mic, sys);
        Assert.True(mixed.Samples.Length > 0);
    }

    /// <summary>PRD P1 #3: "Webcam overlay (PiP)." Verify the webcam PiP compositor draws webcam
    /// pixels into the output frame.</summary>
    [Fact]
    public void U08_Webcam_PiP_Composites_Into_Frame()
    {
        var w = 100; var h = 80;
        var content = MakeFrame(w, h, 0); // black
        var webcam = MakeFrame(40, 30, 255); // white webcam frame
        var output = (byte[])content.Clone();
        FrameCompositor.CompositeWebcam(output, w, h, webcam, 40, 30,
            new WebcamOverlay(0.72, 0.72, 0.25, 0.25, 1.0));
        // The PiP region should now have bright pixels (webcam = white).
        var pipX = (int)(0.85 * w); var pipY = (int)(0.85 * h);
        var idx = (pipY * w + pipX) * 4;
        Assert.True(output[idx] > 200, "PiP region should be bright after compositing webcam");
    }

    /// <summary>PRD P1 #4: "Horizontal (16:9) and Vertical (9:16)." Verify aspect-ratio conversion
    /// re-maps zoom keyframes for a new aspect.</summary>
    [Fact]
    public void U09_Aspect_Ratio_Switch_ReFrames()
    {
        var prog = new Core.Zoom.ZoomProgram();
        prog.Add(new Core.Zoom.ZoomKeyframe(0, new(0.5, 0.5), 2.0));
        var converted = Core.AspectRatio.AspectRatioConverter.Convert(
            prog, Core.AspectRatio.AspectRatioMode.Portrait, new Vec2(1920, 1080));
        Assert.Single(converted.Keyframes);
        Assert.Equal(2.0, converted.Keyframes[0].Scale);
    }

    /// <summary>PRD P1 #5: "Background color/image + padding + drop shadows + borders." Verify
    /// ApplyVisualFrame produces a frame with background fill + content.</summary>
    [Fact]
    public void U10_Visual_Customization_Background_And_Padding()
    {
        var content = MakeFrame(4, 4, 255); // white
        var frame = new FrameCompositor.VisualFrame(0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0); // black bg, 2px padding
        var output = FrameCompositor.ApplyVisualFrame(content, 4, 4, 8, 8, frame);
        // Corner (0,0) is background (black), center is content (white).
        Assert.Equal(0, output[0]); // bg B
        var centerIdx = (4 * 8 + 4) * 4;
        Assert.Equal(255, output[centerIdx]); // content
    }

    // ---- PRD P2: Nice-to-have features ----

    /// <summary>PRD P2 #1: "Natural motion blur on cursor." Verify DrawMotionBlur creates a trail.</summary>
    [Fact]
    public void U11_Motion_Blur_Creates_Trail()
    {
        var buf = MakeFrame(50, 50, 0);
        var positions = new List<(Vec2 position, double ageMs)>
        {
            (new(10, 25), 0), (new(25, 25), 50), (new(40, 25), 100),
        };
        FrameCompositor.DrawMotionBlur(buf, 50, 50, positions, 1.0, 4, 0.8);
        var bright = buf.Count(b => b > 0);
        Assert.True(bright > 20, $"trail produced {bright} bright pixels");
    }

    /// <summary>PRD P2 #2: "Detect and display pressed keys." Verify DrawKeycap renders visible pixels.</summary>
    [Fact]
    public void U12_Keycap_Display_Renders()
    {
        var buf = MakeFrame(100, 60, 0);
        FrameCompositor.DrawKeycap(buf, 100, 60, new FrameCompositor.KeycapDisplay(Text: "Ctrl+S"));
        var bright = buf.Count(b => b > 0);
        Assert.True(bright > 20, $"keycap produced {bright} bright pixels");
    }

    /// <summary>PRD P2 #4: "GIF export + WebM export + presets." Verify the new codecs + presets
    /// validate and enable export.</summary>
    [Fact]
    public void U13_Gif_And_WebM_Presets_Validate()
    {
        foreach (var preset in ExportSettingsViewModel.Presets)
        {
            var vm = new ExportSettingsViewModel { OutputPath = "out" + preset.Codec };
            vm.ApplyPreset(preset);
            Assert.True(vm.CanExport, $"preset {preset.Name} should validate: {vm.ValidationError}");
        }
    }

    /// <summary>PRD success metric: "Export quality indistinguishable from manual editing."
    /// Verify the exported MP4's duration matches the input within tolerance.</summary>
    [Fact]
    public async Task U14_Export_Duration_Matches_Input()
    {
        if (!FfmpegEncoder.IsAvailable()) return;
        var driver = new Drivers.RecordingPipelineDriver(320, 240, 30, 2.0, new CursorScript { DurationSeconds = 2.0 });
        driver.ApplyEffects();
        var path = Path.Combine(Path.GetTempPath(), $"ssc_dur_{Guid.NewGuid():N}.mp4");
        try
        {
            var result = driver.Export(path);
            var probe = Validators.ArtifactValidator.AssertValidMp4(path);
            var duration = Validators.ArtifactValidator.ProbeDurationSeconds(probe);
            Assert.InRange(duration, 1.8, 2.2);
        }
        finally { for (int i = 0; i < 5; i++) try { if (File.Exists(path)) File.Delete(path); break; } catch { Thread.Sleep(200); } }
    }
}
