using ScreenStudio.Core.Audio;

namespace ScreenStudio.Core.Tests.Audio;

/// <summary>Verifies the full audio export path: mic + system tracks through noise-gate →
/// normalize → mix, producing one normalized stereo track at the target rate.</summary>
public class AudioExportMixerTests
{
    private static AudioTrack Track(float amp, int frames, int sr = 48000, int ch = 1)
    {
        var s = new float[frames * ch];
        for (int i = 0; i < s.Length; i++) s[i] = amp;
        return new AudioTrack(s, sr, ch, Gain: 1.0);
    }

    private static AudioTrack NoisyTrack(float signalAmp, float noiseAmp, int frames, int sr = 48000)
    {
        // First half signal, second half noise floor.
        var s = new float[frames];
        var rng = new Random(3);
        for (int i = 0; i < frames / 2; i++) s[i] = signalAmp;
        for (int i = frames / 2; i < frames; i++) s[i] = (float)(noiseAmp * (rng.NextDouble() * 2 - 1));
        return new AudioTrack(s, sr, 1, 1.0);
    }

    [Fact]
    public void MixForExport_Combines_Both_Tracks_And_Normalizes()
    {
        var mic = Track(0.3f, 1000);
        var sys = Track(0.2f, 1000);
        var mixer = new AudioExportMixer(new AudioExportSettings
        {
            EnableNoiseGate = false, EnableNormalization = true, NormalizeTargetPeak = 0.95,
            TargetSampleRate = 48000, TargetChannels = 2,
        });

        var mixed = mixer.MixForExport(mic, sys);

        Assert.Equal(48000, mixed.SampleRate);
        Assert.Equal(2, mixed.Channels);
        // Normalized to the target peak.
        var peak = LevelMeter.Measure(mixed.Samples).PeakAmplitude;
        Assert.Equal(0.95, peak, 3);
    }

    [Fact]
    public void MixForExport_Applies_NoiseGate_To_Mic_Only()
    {
        var mic = NoisyTrack(signalAmp: 0.5f, noiseAmp: 0.01f, frames: 2000);
        var mixer = new AudioExportMixer(new AudioExportSettings
        {
            EnableNoiseGate = true, EnableNormalization = false,
            TargetSampleRate = 48000, TargetChannels = 1,
        });

        var mixed = mixer.MixForExport(mic, systemLoopback: null);

        var sigPeak = LevelMeter.Measure(mixed.Samples.AsSpan(0, 1000)).PeakAmplitude;
        var noisePeak = LevelMeter.Measure(mixed.Samples.AsSpan(1000, 1000)).PeakAmplitude;
        Assert.True(sigPeak > 0.4, "signal preserved");
        Assert.True(noisePeak < 0.01, "noise gated: " + noisePeak);
    }

    [Fact]
    public void MixForExport_Handles_Mic_Only()
    {
        var mic = Track(0.4f, 500);
        var mixer = new AudioExportMixer(new AudioExportSettings { EnableNormalization = false });
        var mixed = mixer.MixForExport(mic, systemLoopback: null);
        Assert.Single(new[] { mixed }); // sanity
        Assert.Equal(0.4f, mixed.Samples[0], 4);
    }

    [Fact]
    public void MixForExport_Handles_System_Only()
    {
        var sys = Track(0.4f, 500);
        // SystemGain defaults to 0.8; set to 1.0 to test pure passthrough.
        var mixer = new AudioExportMixer(new AudioExportSettings { EnableNormalization = false, SystemGain = 1.0 });
        var mixed = mixer.MixForExport(microphone: null, systemLoopback: sys);
        Assert.Equal(0.4f, mixed.Samples[0], 4);
    }

    [Fact]
    public void MixForExport_Both_Empty_Yields_Empty()
    {
        var mixer = new AudioExportMixer(new AudioExportSettings());
        var mixed = mixer.MixForExport(microphone: null, systemLoopback: null);
        Assert.Empty(mixed.Samples);
    }

    [Fact]
    public void MeasureLevel_Reports_Track_Level()
    {
        var t = Track(0.5f, 1000);
        var level = AudioExportMixer.MeasureLevel(t);
        Assert.Equal(0.5, level.PeakAmplitude, 4);
    }
}

