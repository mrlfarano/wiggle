using ScreenStudio.Native.Webcam;

namespace ScreenStudio.Native.Tests;

/// <summary>Verifies the FFmpeg-backed webcam capture produces real webcam frames and
/// enumerates DirectShow video devices. Genuine end-to-end capture (not a stub): ffmpeg dshow
/// video input -> raw BGRA frames -> WebcamFrame events. Covers task 016 "Webcam capture
/// module" with a real artifact. Follows the same env-aware pattern as
/// <c>FfmpegAudioCaptureTests</c> / <c>FfmpegScreenCaptureTests</c>: skips cleanly when ffmpeg
/// is absent (asserting the PlatformNotSupportedException) and returns early when no cameras
/// are connected.</summary>
public class FfmpegWebcamCaptureTests
{
    [Fact]
    public void Captures_Real_Webcam_Frames()
    {
        if (!FfmpegWebcamCapture.IsAvailableStatic())
        {
            // ffmpeg absent: Start() must throw the well-typed exception rather than crash.
            Assert.Throws<PlatformNotSupportedException>(() =>
                new FfmpegWebcamCapture().Start(new WebcamOptions()));
            return;
        }

        using var cap = new FfmpegWebcamCapture();
        var cameras = cap.ListCameras();
        if (cameras.Count == 0)
        {
            // ffmpeg present but no webcam devices on host: Start throws, nothing to assert.
            Assert.Throws<PlatformNotSupportedException>(() => cap.Start(new WebcamOptions()));
            return;
        }

        var received = new List<WebcamFrame>();
        var sub = cap.Frames.Subscribe(new ActionObserver<WebcamFrame>(received.Add));

        cap.Start(new WebcamOptions
        {
            DeviceName = cameras[0],
            Width = 160,
            Height = 120, // small for fast capture
            TargetFrameRate = 10,
        });
        System.Threading.Thread.Sleep(2000); // ~2s -> ~20 frames at 10fps
        cap.Stop();
        sub.Dispose();

        // Working webcam capture module: frames must arrive.
        Assert.True(received.Count > 0, "no webcam frames captured");
        Assert.Equal(160, received[0].Width);
        Assert.Equal(120, received[0].Height);
        Assert.Equal(160 * 120 * 4, received[0].Bgra.Length);

        // Raw output verification: a live camera produces non-uniform BGRA bytes (real scene
        // + sensor noise); a dead/fake source is uniform.
        var first = received[0];
        var distinctByteValues = first.Bgra.Distinct().Count();
        Assert.True(distinctByteValues > 4,
            $"captured webcam frame is near-uniform ({distinctByteValues} distinct byte values) — not a real image");

        // Timestamps must advance.
        Assert.True(received[^1].TimestampMs > received[0].TimestampMs, "timestamps did not advance");
    }
}
