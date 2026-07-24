using ScreenStudio.Core.Audio;

namespace ScreenStudio.Core.Tests.Audio;

public class LevelMeterTests
{
    [Fact]
    public void Silence_Measures_Zero()
    {
        var m = LevelMeter.Measure(new float[100]);
        Assert.Equal(0, m.PeakAmplitude);
        Assert.Equal(0, m.Rms);
        Assert.True(double.IsNegativeInfinity(m.PeakDbFs));
    }

    [Fact]
    public void FullScale_Sine_Has_Correct_Peak_And_Rms()
    {
        // 1 kHz full-scale sine: peak 1.0, RMS = 1/sqrt(2) ≈ 0.7071.
        // Use a whole number of cycles so the windowed RMS is exact: 4800 samples @ 48kHz
        // with 480Hz = 48 complete cycles.
        var n = 4800;
        var buf = new float[n];
        for (int i = 0; i < n; i++) buf[i] = System.MathF.Sin(2f * MathF.PI * 480f * i / 48000f);
        var m = LevelMeter.Measure(buf);
        Assert.Equal(1.0, m.PeakAmplitude, 4);
        Assert.Equal(1.0 / System.Math.Sqrt(2), m.Rms, 3);
    }

    [Fact]
    public void DbFs_Conversions()
    {
        var m = new LevelMeasurement(0.5, 0.5);
        Assert.Equal(-6.0206, m.PeakDbFs, 3); // 20*log10(0.5) = -6.02 dB
        Assert.Equal(-6.0206, m.RmsDbFs, 3);
    }

    [Fact]
    public void NormalizePeak_Scales_To_Target()
    {
        var buf = new float[] { 0.1f, -0.2f, 0.3f, -0.1f };
        var gain = LevelMeter.NormalizePeak(buf, targetPeak: 0.9);
        // New peak should be 0.9.
        var peak = LevelMeter.Measure(buf).PeakAmplitude;
        Assert.Equal(0.9, peak, 5);
        // Gain applied = target/originalPeak = 0.9/0.3 = 3.0
        Assert.Equal(3.0, gain, 5);
    }

    [Fact]
    public void NormalizePeak_Leaves_Silence_Untouched()
    {
        var buf = new float[16];
        var gain = LevelMeter.NormalizePeak(buf, 0.99);
        Assert.Equal(1.0, gain);
        Assert.All(buf, s => Assert.Equal(0f, s));
    }

    [Fact]
    public void NormalizeRms_Matches_Target_Rms()
    {
        var rng = new Random(1);
        var buf = new float[2000];
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)(rng.NextDouble() * 2 - 1) * 0.1f;
        LevelMeter.NormalizeRms(buf, targetRms: 0.3);
        var rms = LevelMeter.Measure(buf).Rms;
        Assert.Equal(0.3, rms, 2);
    }
}

public class NoiseGateTests
{
    private static float[] Tone(int n, double amp, double freqHz, int sr = 48000)
    {
        var b = new float[n];
        for (int i = 0; i < n; i++)
            b[i] = (float)(amp * System.Math.Sin(2 * System.Math.PI * freqHz * i / sr));
        return b;
    }

    [Fact]
    public void Attenuates_Below_Threshold_Preserves_Above()
    {
        // First half: loud tone (amp 0.5). Second half: quiet noise floor (amp 0.005).
        var loud = Tone(2000, 0.5, 1000);
        var quiet = Tone(2000, 0.005, 1000);
        var buf = loud.Concat(quiet).ToArray();

        var gate = new NoiseGate
        {
            Threshold = 0.02,
            Channels = 1,
            AttackSeconds = 0.001,
            ReleaseSeconds = 0.020,
        };
        gate.Process(buf, 48000);

        var loudPeak = LevelMeter.Measure(buf.AsSpan(0, 2000)).PeakAmplitude;
        var quietPeak = LevelMeter.Measure(buf.AsSpan(2000, 2000)).PeakAmplitude;

        // Loud half stays near full level.
        Assert.True(loudPeak > 0.4, $"loud peak {loudPeak} should be preserved");
        // Quiet half is gated down (floor=0 => near silence after release).
        Assert.True(quietPeak < 0.01, $"quiet peak {quietPeak} should be gated down");
    }

    [Fact]
    public void No_Clicks_Release_Smooths_To_Floor()
    {
        // A long release must not zero the signal abruptly (gain ramps, not steps).
        var buf = Tone(4800, 0.4, 1000); // 0.1s
        // Drop input to silence after frame 2400; gate should ramp down, not jump.
        for (int i = 2400; i < buf.Length; i++) buf[i] = 0f;

        var gate = new NoiseGate { Threshold = 0.1, ReleaseSeconds = 0.05, Channels = 1 };
        gate.Process(buf, 48000);

        // The output should monotonically (roughly) decay in peak amplitude after the cutoff.
        var early = LevelMeter.Measure(buf.AsSpan(0, 2400)).PeakAmplitude;
        var mid = LevelMeter.Measure(buf.AsSpan(2400, 1200)).PeakAmplitude;
        var late = LevelMeter.Measure(buf.AsSpan(3600, 1200)).PeakAmplitude;
        Assert.True(mid < early, "release region should be quieter than active region");
        Assert.True(late <= mid + 1e-3, "late region should not be louder than mid (smooth release)");
    }

    [Fact]
    public void Reset_Restores_Open_State()
    {
        var gate = new NoiseGate { Threshold = 0.5 };
        var buf = new float[1000]; // silence
        gate.Process(buf, 48000);
        Assert.False(gate.GetType().GetField("_open", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(gate) is true);
        gate.Reset();
        var open = gate.GetType().GetField("_open", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(gate);
        Assert.True((bool)open!);
    }
}

public class AudioMixerTests
{
    private static AudioTrack Track(float amp, int frames, int sr = 48000, int ch = 1, double gain = 1.0)
    {
        var s = new float[frames * ch];
        for (int i = 0; i < s.Length; i++) s[i] = amp;
        return new AudioTrack(s, sr, ch, gain);
    }

    [Fact]
    public void Mix_Sums_Overlapping_Samples()
    {
        var mic = Track(0.3f, 1000);
        var sys = Track(0.2f, 1000);
        var mixed = AudioMixer.Mix(new[] { mic, sys }, 48000, 1);
        Assert.Equal(1000, mixed.Samples.Length);
        // 0.3 + 0.2 = 0.5 (below clip threshold).
        Assert.Equal(0.5f, mixed.Samples[0], 4);
    }

    [Fact]
    public void Mix_Pads_Shorter_Track()
    {
        var long_ = Track(0.4f, 2000);
        var short_ = Track(0.1f, 1000);
        var mixed = AudioMixer.Mix(new[] { long_, short_ }, 48000, 1);
        Assert.Equal(2000, mixed.Samples.Length);
        Assert.Equal(0.5f, mixed.Samples[0], 4);   // both contribute
        Assert.Equal(0.4f, mixed.Samples[1500], 4); // only the long track
    }

    [Fact]
    public void Mix_SoftClips_On_Overflow()
    {
        var t = Track(0.9f, 500);
        var mixed = AudioMixer.Mix(new[] { t, t }, 48000, 1);
        // Sum is 1.8 — must be soft-clipped to <= ~1.0, not hard clipped or overflowed.
        Assert.True(mixed.Samples[0] <= 1.0f, "soft clip should bound to <= 1");
        Assert.True(mixed.Samples[0] > 0.99f, "soft clip near saturation should be ~1");
    }

    [Fact]
    public void Mix_Resamples_To_Target_Rate()
    {
        // Track authored at 24000 Hz; mix down to 48000 Hz => output should be 2x frames.
        var t = Track(0.5f, 1000, sr: 24000);
        var mixed = AudioMixer.Mix(new[] { t }, 48000, 1);
        Assert.Equal(2000, mixed.Samples.Length);
        Assert.Equal(0.5f, mixed.Samples[0], 4);
    }

    [Fact]
    public void Mix_Upmixes_Mono_To_Stereo()
    {
        var mono = Track(0.5f, 1000, ch: 1);
        var mixed = AudioMixer.Mix(new[] { mono }, 48000, 2);
        Assert.Equal(2000, mixed.Samples.Length);
        Assert.Equal(0.5f, mixed.Samples[0], 4); // left
        Assert.Equal(0.5f, mixed.Samples[1], 4); // right
    }

    [Fact]
    public void Mix_Applies_Per_Track_Gain()
    {
        var a = Track(0.5f, 100, gain: 2.0);  // contributes 0.5*2 = 1.0... clipped
        var b = Track(0.1f, 100, gain: 0.5);  // contributes 0.1*0.5 = 0.05
        var mixed = AudioMixer.Mix(new[] { a, b }, 48000, 1, masterGain: 1.0);
        // 1.0 + 0.05 = 1.05 soft-clips to ~1.0
        Assert.True(mixed.Samples[0] <= 1.0f);
    }

    [Fact]
    public void Empty_Track_List_Yields_Empty_Output()
    {
        var mixed = AudioMixer.Mix(Array.Empty<AudioTrack>(), 48000, 2);
        Assert.Empty(mixed.Samples);
    }
}
