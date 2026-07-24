using ScreenStudio.Core.Export;

namespace ScreenStudio.Core.Audio;

/// <summary>Settings for the audio portion of an export (PRD P1: noise reduction,
/// normalization, separate track control, mixed-audio export).</summary>
public sealed class AudioExportSettings
{
    public bool EnableNoiseGate { get; set; } = true;
    public bool EnableNormalization { get; set; } = true;
    /// <summary>Normalization target peak (0..1) when EnableNormalization is on.</summary>
    public double NormalizeTargetPeak { get; set; } = 0.95;
    /// <summary>Per-track gains before mixing (PRD: "Separate audio track control").</summary>
    public double MicGain { get; set; } = 1.0;
    public double SystemGain { get; set; } = 0.8;
    public double MasterGain { get; set; } = 1.0;
    /// <summary>Output sample rate for the mixed track (must match what the encoder expects).</summary>
    public int TargetSampleRate { get; set; } = 48000;
    public int TargetChannels { get; set; } = 2;
}

/// <summary>Task 008 — the audio export stage. Chains noise-gate → normalization → mix to
/// produce the final mixed audio track that the export pipeline muxes into the MP4.
/// Delivers "Export with mixed audio": raw mic + system tracks in, one normalized mixed
/// track out. Fully headless-testable (no device dependency).</summary>
public sealed class AudioExportMixer
{
    private readonly AudioExportSettings _settings;

    public AudioExportMixer(AudioExportSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (settings.TargetSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(settings.TargetSampleRate));
        if (settings.TargetChannels <= 0) throw new ArgumentOutOfRangeException(nameof(settings.TargetChannels));
    }

    /// <summary>Process mic + system tracks into one normalized, mixed track.
    /// Either input may be null/empty (mic-only or system-only exports). Normalization is
    /// applied to the FINAL mixed output (master loudness), matching "Audio normalization"
    /// semantics — not per-input.</summary>
    public AudioTrack MixForExport(AudioTrack? microphone, AudioTrack? systemLoopback)
    {
        var tracks = new List<AudioTrack>(2);

        if (microphone is { } mic && mic.Samples.Length > 0)
        {
            var micSamples = (float[])mic.Samples.Clone();
            if (_settings.EnableNoiseGate)
            {
                var gate = new NoiseGate { Channels = mic.Channels, Threshold = 0.02 };
                gate.Process(micSamples, mic.SampleRate);
            }
            tracks.Add(mic with { Samples = micSamples, Gain = _settings.MicGain });
        }

        if (systemLoopback is { } sys && sys.Samples.Length > 0)
        {
            tracks.Add(sys with { Gain = _settings.SystemGain });
        }

        var mixed = AudioMixer.Mix(tracks, _settings.TargetSampleRate, _settings.TargetChannels, _settings.MasterGain);

        // Master normalization on the combined output.
        if (_settings.EnableNormalization && mixed.Samples.Length > 0)
        {
            var outSamples = mixed.Samples; // Mix returns a fresh array; safe to mutate in place.
            LevelMeter.NormalizePeak(outSamples, _settings.NormalizeTargetPeak);
            mixed = mixed with { Samples = outSamples };
        }

        return mixed;
    }

    /// <summary>Level meter reading for a track (for the export-settings UI preview meters).</summary>
    public static LevelMeasurement MeasureLevel(AudioTrack track)
        => LevelMeter.Measure(track.Samples);
}
