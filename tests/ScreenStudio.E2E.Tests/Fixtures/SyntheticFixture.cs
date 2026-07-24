using ScreenStudio.Core.Audio;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;

namespace ScreenStudio.E2E.Tests.Fixtures;

/// <summary>Deterministic test-data builders. No real screen/mic — these feed the same
/// downstream engine types the real capture feeds, so the E2E suite is CI-portable + fast.</summary>
public static class SyntheticFixture
{
    /// <summary>A distinct, non-uniform BGRA frame. Each frame differs by index so the
    /// compositor/encoder can't hide blank or duplicated output. Deterministic (seeded).</summary>
    public static byte[] GradientFrame(int width, int height, int frameIndex, int seed = 1)
    {
        var buf = new byte[width * height * 4]; // BGRA
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                // Cheap deterministic hash mixing x, y, frameIndex, seed → distinct per frame.
                buf[i + 0] = (byte)(((x + frameIndex) * 7 + seed) & 0xFF);        // B
                buf[i + 1] = (byte)(((y + frameIndex / 2) * 11 + seed * 3) & 0xFF); // G
                buf[i + 2] = (byte)(((x ^ y) + frameIndex * 13 + seed) & 0xFF);     // R
                buf[i + 3] = 0xFF;                                                  // A
            }
        return buf;
    }

    /// <summary>Build a scripted cursor stream: linear Start→End path at SampleRateHz, with
    /// injected clicks (button-down pinned at click time/position) and pauses (held position),
    /// plus seeded micro-jitter. Returns events sorted ascending by TimestampMs.</summary>
    public static List<CursorEvent> CursorStream(CursorScript script)
    {
        var rng = new Random(script.Seed);
        var dtMs = 1000.0 / script.SampleRateHz;
        var totalSamples = (int)(script.DurationSeconds * script.SampleRateHz);
        var events = new List<CursorEvent>(totalSamples + script.Clicks.Count);

        // Base path: linear interpolation Start→End over the duration.
        for (int s = 0; s < totalSamples; s++)
        {
            var u = (double)s / (totalSamples - 1);
            var x = script.Start.X + (script.End.X - script.Start.X) * u;
            var y = script.Start.Y + (script.End.Y - script.Start.Y) * u;
            // Apply jitter unless this sample falls inside a pause window.
            var tMs = s * dtMs;
            var inPause = false;
            foreach (var (pTime, pDur) in script.Pauses)
            {
                if (tMs >= pTime && tMs <= pTime + pDur) { inPause = true; break; }
            }
            if (!inPause)
            {
                x += (rng.NextDouble() - 0.5) * script.Jitter;
                y += (rng.NextDouble() - 0.5) * script.Jitter;
            }
            events.Add(new CursorEvent(tMs, new Vec2(x, y), CursorButtonState.None));
        }

        // Inject clicks: set button-down + snap position to the scripted click location.
        foreach (var (clickTime, clickPos) in script.Clicks)
        {
            // Find the nearest base-path sample and override it with the click.
            var idx = (int)Math.Round(clickTime / dtMs);
            if (idx < 0) idx = 0;
            if (idx >= events.Count) idx = events.Count - 1;
            events[idx] = events[idx] with { Position = clickPos, Buttons = CursorButtonState.Left };
        }

        events.Sort((a, b) => a.TimestampMs.CompareTo(b.TimestampMs));
        return events;
    }

    /// <summary>A pure-tone audio track of a given duration. Used as mic/system source for
    /// mix tests. Interleaved float PCM at the requested sample rate/channels.</summary>
    public static AudioTrack ToneTrack(
        int sampleRate, int channels, double seconds,
        double amplitude = 0.3, int freqHz = 440, int seed = 1)
    {
        var frames = (int)(seconds * sampleRate);
        var samples = new float[frames * channels];
        for (int f = 0; f < frames; f++)
        {
            var t = (double)f / sampleRate;
            var sample = amplitude * Math.Sin(2 * Math.PI * freqHz * t);
            for (int c = 0; c < channels; c++)
                samples[f * channels + c] = (float)sample;
        }
        return new AudioTrack(samples, sampleRate, channels, Gain: 1.0);
    }

    /// <summary>An audio track that's signal (amplitude) for the first half, low-amplitude
    /// noise floor for the second half — for the noise-gate test (E10).</summary>
    public static AudioTrack SignalPlusNoiseTrack(
        int sampleRate, int channels, double seconds,
        double signalAmp = 0.4, double noiseAmp = 0.005, int seed = 3)
    {
        var frames = (int)(seconds * sampleRate);
        var samples = new float[frames * channels];
        var rng = new Random(seed);
        var half = frames / 2;
        for (int f = 0; f < frames; f++)
        {
            double s = f < half
                ? signalAmp * Math.Sin(2 * Math.PI * 440 * (double)f / sampleRate)
                : noiseAmp * (rng.NextDouble() * 2 - 1);
            for (int c = 0; c < channels; c++)
                samples[f * channels + c] = (float)s;
        }
        return new AudioTrack(samples, sampleRate, channels, Gain: 1.0);
    }
}
