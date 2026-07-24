using SysMath = System.Math;

namespace ScreenStudio.Core.Audio;

/// <summary>One audio track for mixing (PRD P1: "Separate audio track control").
/// Tracks carry their own PCM, sample rate, channel count, and a per-track gain.</summary>
public readonly record struct AudioTrack(float[] Samples, int SampleRate, int Channels, double Gain)
{
    public double DurationSeconds => SampleRate > 0 ? (double)Samples.Length / Channels / SampleRate : 0;
}

/// <summary>Task 008 — audio mixer (PRD P1: "Export with mixed audio" + "Separate audio
/// track control"). Combines multiple tracks (e.g. microphone + system loopback) into a
/// single interleaved float[] output at a target sample rate / channel count. Tracks are
/// resampled by nearest-neighbor if their rate differs from the target (sufficient for
/// preview; a sinc resampler is a follow-on).</summary>
public static class AudioMixer
{
    /// <summary>Mix tracks into a single interleaved buffer at the target rate/channels.
    /// Applies soft clipping to prevent sum overflow. Tracks shorter than the longest are
    /// zero-padded (they simply stop contributing past their end).</summary>
    public static AudioTrack Mix(IReadOnlyList<AudioTrack> tracks, int targetSampleRate, int targetChannels, double masterGain = 1.0)
    {
        if (targetSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(targetSampleRate));
        if (targetChannels <= 0) throw new ArgumentOutOfRangeException(nameof(targetChannels));
        if (tracks.Count == 0)
            return new AudioTrack(Array.Empty<float>(), targetSampleRate, targetChannels, masterGain);

        // Compute the longest track's duration to size the output (in target frames).
        double maxSeconds = 0;
        foreach (var t in tracks) maxSeconds = SysMath.Max(maxSeconds, t.DurationSeconds);
        var totalFrames = (int)SysMath.Ceiling(maxSeconds * targetSampleRate);
        var output = new float[totalFrames * targetChannels];

        // Allocate the per-frame mix accumulator once (outside the loop) — it's reset each frame.
        Span<double> mix = targetChannels <= 64
            ? stackalloc double[targetChannels]
            : new double[targetChannels];

        for (int outFrame = 0; outFrame < totalFrames; outFrame++)
        {
            double outTime = (double)outFrame / targetSampleRate;
            mix.Clear();

            foreach (var track in tracks)
            {
                if (track.SampleRate <= 0 || track.Channels <= 0 || track.Samples.Length == 0) continue;
                var srcFrame = (int)(outTime * track.SampleRate);
                var srcFrames = track.Samples.Length / track.Channels;
                if (srcFrame < 0 || srcFrame >= srcFrames) continue;

                // Map track channels to output channels with proper down/up-mix:
                //  - same count: 1:1
                //  - mono -> stereo (or more): copy to every output channel
                //  - N -> M (N>M): downmix by averaging N across M
                //  - N -> M (1<N<M): spread, duplicating as needed
                if (track.Channels == 1)
                {
                    var s = track.Samples[srcFrame] * track.Gain;
                    for (int oc = 0; oc < targetChannels; oc++) mix[oc] += s;
                }
                else if (track.Channels == targetChannels)
                {
                    for (int c = 0; c < targetChannels; c++)
                        mix[c] += track.Samples[srcFrame * track.Channels + c] * track.Gain;
                }
                else if (track.Channels > targetChannels)
                {
                    // Downmix: average all source channels into each output channel equally.
                    double sum = 0;
                    for (int tc = 0; tc < track.Channels; tc++)
                        sum += track.Samples[srcFrame * track.Channels + tc];
                    sum = sum / track.Channels * track.Gain;
                    for (int oc = 0; oc < targetChannels; oc++) mix[oc] += sum;
                }
                else
                {
                    // Upmix (1 < src < target): round-robin copy with duplication.
                    for (int tc = 0; tc < track.Channels; tc++)
                    {
                        var s = track.Samples[srcFrame * track.Channels + tc] * track.Gain;
                        mix[tc % targetChannels] += s;
                    }
                }
            }

            // Apply master gain + soft clip (tanh) to avoid hard clipping distortion.
            for (int c = 0; c < targetChannels; c++)
            {
                var v = mix[c] * masterGain;
                output[outFrame * targetChannels + c] = SoftClip(v);
            }
        }

        return new AudioTrack(output, targetSampleRate, targetChannels, masterGain);
    }

    /// <summary>Soft clipper: a smooth saturator that approaches ±1 asymptotically, reaching
    /// ~0.98 at |x|=1.8 and ~0.995 at |x|=4. Below the ±1 threshold it's linear (transparent).
    /// Cheaper than tanh while avoiding the harshness of hard clipping.</summary>
    private static float SoftClip(double x)
    {
        const double a = 1.0;
        var ax = SysMath.Abs(x);
        if (ax <= a) return (float)x;                       // linear region, no distortion
        // Saturation: out = sign * a * (1 + ln(1 + (|x|-a)/a) / 2). Approaches 1 slowly.
        var sat = a * (1.0 + SysMath.Log(1.0 + (ax - a) / a) * 0.5);
        if (sat > 0.999) sat = 0.999;                       // bound just under full scale
        return (float)(SysMath.Sign(x) * sat);
    }
}
