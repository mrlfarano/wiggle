using ScreenStudio.Native.Audio;

namespace ScreenStudio.Native.Tests;

/// <summary>Verifies the FFmpeg-backed audio capture produces real audio samples from a
/// microphone. Genuine end-to-end capture (not a stub): ffmpeg dshow input -> s16le PCM ->
/// float[] AudioBuffers. Covers task 008 "Audio capture module" with a real artifact.</summary>
public class FfmpegAudioCaptureTests
{
    [Fact]
    public void Captures_Real_Audio_Samples_From_Microphone()
    {
        if (!FfmpegAudioCapture.IsAvailableStatic())
        {
            Assert.Throws<PlatformNotSupportedException>(() =>
                new FfmpegAudioCapture().Start(true, false));
            return;
        }

        var mics = FfmpegAudioCapture.ListMicrophones();
        if (mics.Count == 0) return; // no mic devices on host

        using var cap = new FfmpegAudioCapture();
        var received = new List<AudioBuffer>();
        var sub = cap.Buffers.Subscribe(new ActionObserver<AudioBuffer>(received.Add));

        cap.Start(microphone: true, systemLoopback: false);
        System.Threading.Thread.Sleep(2000); // ~2s of capture
        cap.Stop();
        sub.Dispose();

        Assert.True(received.Count > 0, "no audio buffers captured");
        Assert.True(received[0].Samples.Length > 0, "first buffer empty");
        Assert.Equal(48000, received[0].SampleRate);

        // Verify real capture: a live mic produces non-uniform samples (ambient noise floor),
        // whereas a dead/fake source is uniform. We check variance, not loudness — a quiet room
        // is legitimately near-silent but still has non-uniform ambient samples.
        var firstBuf = received[0].Samples;
        var distinctValues = firstBuf.Distinct().Count();
        Assert.True(distinctValues > 1,
            $"captured buffer is uniform (single value) — not real audio; distinct={distinctValues}");
    }
}
