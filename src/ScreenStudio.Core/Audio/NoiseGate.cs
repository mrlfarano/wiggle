using SysMath = System.Math;

namespace ScreenStudio.Core.Audio;

/// <summary>Task 008 — noise-gate DSP (PRD P1: "Background noise reduction").
/// A downward expander that attenuates signal below a threshold, with attack/release
/// smoothing so the gate opens/closes without clicks. Operates on interleaved float[]
/// PCM in-place; stateful across buffers (real-time streaming).</summary>
public sealed class NoiseGate
{
    /// <summary>Linear amplitude threshold below which the gate starts closing. 0..1.</summary>
    public double Threshold { get; set; } = 0.02; // ~-34 dBFS for a 1.0-full-scale signal

    /// <summary>How far below the threshold the signal must drop before the gate re-opens
    /// (hysteresis) to avoid chatter at the threshold edge. 0..1.</summary>
    public double Hysteresis { get; set; } = 0.01;

    /// <summary>Seconds for the gain to ramp from closed→open (avoids click on onset).</summary>
    public double AttackSeconds { get; set; } = 0.005;

    /// <summary>Seconds for the gain to ramp open→closed (avoids click on tail truncation).</summary>
    public double ReleaseSeconds { get; set; } = 0.050;

    /// <summary>Attenuation applied when fully closed (0 = silence, 1 = no gating).</summary>
    public double FloorGain { get; set; } = 0.0;

    /// <summary>Channels in the interleaved stream. Set once; must match all buffers.</summary>
    public int Channels { get; set; } = 1;

    private double _currentGain = 1.0;
    private bool _open = true;

    /// <summary>Process a buffer in place. <paramref name="samples"/> is interleaved PCM,
    /// length must be a multiple of <see cref="Channels"/>.</summary>
    public void Process(Span<float> samples, int sampleRate)
    {
        if (Channels < 1) throw new InvalidOperationException("Channels must be >= 1");
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (samples.Length % Channels != 0)
            throw new ArgumentException($"sample count {samples.Length} is not a multiple of {Channels} channels");

        var attackStep = AttackSeconds > 0 ? 1.0 / (AttackSeconds * sampleRate) : 1.0;
        var releaseStep = ReleaseSeconds > 0 ? 1.0 / (ReleaseSeconds * sampleRate) : 1.0;

        for (int i = 0; i < samples.Length; i += Channels)
        {
            // Per-frame level: max abs across channels (a loud channel holds the gate open).
            double level = 0;
            for (int c = 0; c < Channels; c++)
                level = SysMath.Max(level, SysMath.Abs(samples[i + c]));

            // Hysteresis state machine: open above threshold, close below (threshold - hysteresis).
            if (!_open && level >= Threshold) _open = true;
            else if (_open && level < Threshold - Hysteresis) _open = false;

            var target = _open ? 1.0 : FloorGain;
            // Ramp toward target using whichever coefficient applies.
            var step = target > _currentGain ? attackStep : releaseStep;
            _currentGain += (target - _currentGain) * SysMath.Min(1.0, step);
            // Allow a fast initial open if attack is effectively zero.
            if (step >= 1.0) _currentGain = target;

            // Apply per-sample gain.
            var g = (float)_currentGain;
            for (int c = 0; c < Channels; c++)
                samples[i + c] *= g;
        }
    }

    /// <summary>Reset internal envelope state (call between independent streams).</summary>
    public void Reset() { _currentGain = 1.0; _open = true; }
}
