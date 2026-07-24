using SysMath = System.Math;

namespace ScreenStudio.Core.Audio;

/// <summary>Per-buffer level measurement (PRD P1: "Volume level meters").
/// Computes peak and RMS over a buffer; the UI binds to these for the meter bars.</summary>
public readonly record struct LevelMeasurement(double PeakAmplitude, double Rms)
{
    /// <summary>Peak in dBFS (full-scale = 1.0 => 0 dB). Returns -∞ for silence.</summary>
    public double PeakDbFs => PeakAmplitude <= 0 ? double.NegativeInfinity : 20.0 * SysMath.Log10(PeakAmplitude);

    /// <summary>RMS in dBFS.</summary>
    public double RmsDbFs => Rms <= 0 ? double.NegativeInfinity : 20.0 * SysMath.Log10(Rms);
}

/// <summary>Task 008 — level metering + normalization (PRD P1: "Volume level meters",
/// "Audio normalization"). Stateless DSP over float[] PCM.</summary>
public static class LevelMeter
{
    /// <summary>Measure peak amplitude and RMS of a buffer (any channel count; interleaved).</summary>
    public static LevelMeasurement Measure(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return new LevelMeasurement(0, 0);
        double peakSq = 0;
        double sumSq = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            var s = samples[i];
            var a = SysMath.Abs(s);
            if (a * a > peakSq) peakSq = a * a;
            sumSq += (double)s * s;
        }
        var rms = SysMath.Sqrt(sumSq / samples.Length);
        return new LevelMeasurement(SysMath.Sqrt(peakSq), rms);
    }

    /// <summary>Peak-normalize a buffer in place so its peak reaches <paramref name="targetPeak"/>.
    /// targetPeak is linear amplitude (1.0 = full scale). Returns the applied gain.</summary>
    public static double NormalizePeak(Span<float> samples, double targetPeak = 0.99)
    {
        if (targetPeak <= 0 || targetPeak > 1) throw new ArgumentOutOfRangeException(nameof(targetPeak));
        var peak = Measure(samples).PeakAmplitude;
        if (peak <= 1e-9) return 1.0; // silence — leave untouched, gain of 1 (no amplification of nothing)
        var gain = targetPeak / peak;
        for (int i = 0; i < samples.Length; i++) samples[i] = (float)(samples[i] * gain);
        return gain;
    }

    /// <summary>RMS-normalize a buffer so its RMS reaches <paramref name="targetRms"/>.
    /// Useful for matching perceived loudness across clips.</summary>
    public static double NormalizeRms(Span<float> samples, double targetRms = 0.3)
    {
        if (targetRms <= 0) throw new ArgumentOutOfRangeException(nameof(targetRms));
        var rms = Measure(samples).Rms;
        if (rms <= 1e-9) return 1.0;
        var gain = targetRms / rms;
        for (int i = 0; i < samples.Length; i++) samples[i] = (float)(samples[i] * gain);
        // Clamp any overshoot from the gain push.
        for (int i = 0; i < samples.Length; i++)
            samples[i] = SysMath.Clamp(samples[i], -1f, 1f);
        return gain;
    }
}
