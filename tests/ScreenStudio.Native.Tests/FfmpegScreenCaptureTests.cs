using System.Runtime.InteropServices;
using ScreenStudio.Native.Capture;

namespace ScreenStudio.Native.Tests;

/// <summary>Verifies the FFmpeg-backed screen capture produces real desktop frames. Genuine
/// end-to-end capture (not a stub): ffmpeg gdigrab -> raw BGRA frames -> CapturedFrame events.
/// Covers task 002 "Working screen capture module" and "Raw output verification".</summary>
public class FfmpegScreenCaptureTests
{
    [Fact]
    public void Captures_Real_Desktop_Frames()
    {
        using var cap = new FfmpegScreenCapture();
        if (!cap.IsAvailable())
        {
            Assert.Throws<PlatformNotSupportedException>(() => cap.Start(new CaptureOptions()));
            return;
        }

        var received = new List<CapturedFrame>();
        var sub = cap.Frames.Subscribe(new ActionObserver<CapturedFrame>(received.Add));

        cap.Start(new CaptureOptions
        {
            Target = CaptureTarget.FullScreen,
            RegionSize = new(320, 240), // small for fast capture
            TargetFrameRate = 10,
        });
        System.Threading.Thread.Sleep(2000); // ~2s -> ~20 frames at 10fps
        cap.Stop();
        sub.Dispose();

        // Working screen capture module: frames must arrive.
        Assert.True(received.Count > 0, "no screen frames captured");
        Assert.Equal(320, received[0].Width);
        Assert.Equal(240, received[0].Height);

        // Raw output verification: the captured BGRA bytes must be non-uniform (a real desktop
        // has varying pixels; a dead/fake source would be uniform).
        var first = received[0];
        var bytes = new byte[first.Width * first.Height * 4];
        Marshal.Copy(first.TextureHandle, bytes, 0, bytes.Length);
        var distinctByteValues = bytes.Distinct().Count();
        Assert.True(distinctByteValues > 4,
            $"captured frame is near-uniform ({distinctByteValues} distinct byte values) — not a real desktop image");

        // Timestamps must advance.
        Assert.True(received[^1].TimestampMs > received[0].TimestampMs, "timestamps did not advance");
    }
}
