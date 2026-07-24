using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScreenStudio.Core.AspectRatio;

namespace ScreenStudio.Core.Export;

/// <summary>Task 006 — Export settings ViewModel (the MVVM core the Export settings UI binds to).
/// Wraps <see cref="ExportSettings"/> with change notification, named presets, and live
/// validation that the view surfaces as enable/disable on the Export button.
///
/// This is the testable layer behind the WinUI XAML (which requires VS Build Tools to compile
/// in this environment). The view is a thin binding surface over this ViewModel.</summary>
public sealed class ExportSettingsViewModel : INotifyPropertyChanged
{
    private readonly ExportSettings _settings = new();
    private string _validationError = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ---- Bound properties (two-way from the UI) ----

    public VideoCodec Codec
    {
        get => _settings.Codec;
        set { _settings.Codec = value; Raise(); RecomputeValidation(); }
    }

    public int Width
    {
        get => _settings.Resolution.Width;
        set { _settings.Resolution = new(value, _settings.Resolution.Height); Raise(); Raise(nameof(Height)); RecomputeValidation(); }
    }

    public int Height
    {
        get => _settings.Resolution.Height;
        set { _settings.Resolution = new(_settings.Resolution.Width, value); Raise(nameof(Width)); Raise(); RecomputeValidation(); }
    }

    public int FrameRate
    {
        get => _settings.FrameRate;
        set { _settings.FrameRate = value; Raise(); RecomputeValidation(); }
    }

    public int BitrateKbps
    {
        get => _settings.BitrateKbps;
        set { _settings.BitrateKbps = value; Raise(); RecomputeValidation(); }
    }

    public bool UseHardwareEncoding
    {
        get => _settings.UseHardwareEncoding;
        set { _settings.UseHardwareEncoding = value; Raise(); }
    }

    public AspectRatioMode AspectRatio
    {
        get => _settings.AspectRatio;
        set { _settings.AspectRatio = value; Raise(); }
    }

    public string OutputPath
    {
        get => _settings.OutputPath;
        set { _settings.OutputPath = value; Raise(); RecomputeValidation(); }
    }

    // ---- Derived/validation state ----

    /// <summary>Empty when valid; otherwise a user-facing message. The Export button is
    /// enabled iff this is empty.</summary>
    public string ValidationError
    {
        get => _validationError;
        private set { _validationError = value; Raise(); Raise(nameof(CanExport)); }
    }

    public bool CanExport => string.IsNullOrEmpty(_validationError);

    /// <summary>The underlying settings snapshot (passed to the export pipeline).</summary>
    public ExportSettings Snapshot => _settings;

    // ---- Presets (PRD: "Resolution options", social presets) ----

    /// <summary>Named export presets the UI offers as one-click choices.
    /// The first block covers the original H.264/HEVC MP4 presets; the second block adds
    /// the social/advanced presets from PRD P2 #4 (task 020): a vertical TikTok clip,
    /// a high-quality YouTube master, a looping GIF, and a WebM clip.</summary>
    public static readonly ExportPreset[] Presets =
    {
        // --- MP4 family (H.264 / HEVC) ---
        new("1080p / 60fps",  VideoCodec.H264, Resolution.HD1080p, 60, 12_000),
        new("1080p / 30fps",  VideoCodec.H264, Resolution.HD1080p, 30, 8_000),
        new("720p / 60fps",   VideoCodec.H264, Resolution.HD720p,  60, 6_000),
        new("4K / 60fps",     VideoCodec.H264, Resolution.UHD4K,    60, 40_000),
        new("4K / 30fps",     VideoCodec.HEVC, Resolution.UHD4K,    30, 25_000),

        // --- Social / advanced presets (task 020: PRD P2 #4) ---
        // 9:16 vertical at 720p for TikTok / Reels / Shorts. 720x1280 @ 30fps, H.264.
        new("TikTok 9:16 720p", VideoCodec.H264, new(720, 1280), 30, 6_000),
        // 16:9 master for YouTube, 1080p @ 60fps, generous bitrate.
        new("YouTube 1080p",    VideoCodec.H264, Resolution.HD1080p, 60, 12_000),
        // Looping GIF at 480p, 15fps (small file, animation-friendly). Bitrate unused by GIF.
        new("GIF 480p loop",    VideoCodec.GIF,  Resolution.SD480p, 15, 1_000),
        // WebM/VP9 clip at 720p, 30fps, ~1 Mbps.
        new("WebM 720p",        VideoCodec.WEBM, Resolution.HD720p, 30, 1_000),
    };

    public void ApplyPreset(ExportPreset preset)
    {
        _settings.Codec = preset.Codec;
        _settings.Resolution = preset.Resolution;
        _settings.FrameRate = preset.FrameRate;
        _settings.BitrateKbps = preset.BitrateKbps;
        Raise(nameof(Codec)); Raise(nameof(Width)); Raise(nameof(Height));
        Raise(nameof(FrameRate)); Raise(nameof(BitrateKbps));
        RecomputeValidation();
    }

    private void RecomputeValidation()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.OutputPath))
                throw new InvalidOperationException("Output path is required.");
            _settings.Validate();
            ValidationError = string.Empty;
        }
        catch (Exception ex) { ValidationError = ex.Message; }
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A named, one-click export preset.</summary>
public readonly record struct ExportPreset(
    string Name, VideoCodec Codec, Resolution Resolution, int FrameRate, int BitrateKbps);
