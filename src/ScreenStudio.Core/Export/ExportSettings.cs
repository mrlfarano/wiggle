using ScreenStudio.Core.AspectRatio;

namespace ScreenStudio.Core.Export;

/// <summary>Codec choice. H.264 is the MVP (PRD P0); HEVC is a stretch option.
/// GIF and WEBM were added in task 020 (PRD P2 #4: Advanced Export).</summary>
public enum VideoCodec { H264, HEVC, GIF, WEBM }

/// <summary>Resolution preset (PRD P0: "Resolution options (1080p, 4K)").</summary>
public readonly record struct Resolution(int Width, int Height)
{
    public static readonly Resolution HD1080p = new(1920, 1080);
    public static readonly Resolution UHD4K   = new(3840, 2160);
    public static readonly Resolution HD720p  = new(1280, 720);
    /// <summary>480p — natural fit for GIF exports (task 020).</summary>
    public static readonly Resolution SD480p  = new(854, 480);
}

/// <summary>Export settings (task 006). Mirrors the export-settings UI deliverable and
/// feeds the export pipeline. All fields are plain data so the UI can two-way bind.</summary>
public sealed class ExportSettings
{
    public VideoCodec Codec { get; set; } = VideoCodec.H264;
    public Resolution Resolution { get; set; } = Resolution.HD1080p;
    public int FrameRate { get; set; } = 60;          // PRD: 30 or 60
    public int BitrateKbps { get; set; } = 12_000;    // quality/bitrate control
    public bool UseHardwareEncoding { get; set; } = true; // Media Foundation NVENC/QSV/AMF when available
    public AspectRatioMode AspectRatio { get; set; } = AspectRatioMode.Landscape;
    public string OutputPath { get; set; } = "export.mp4";

    /// <summary>Estimated output frame count for the given duration.</summary>
    public int FrameCount(double durationMs) => (int)System.Math.Ceiling(durationMs / 1000.0 * FrameRate);

    public void Validate()
    {
        if (Resolution.Width <= 0 || Resolution.Height <= 0)
            throw new InvalidOperationException("Invalid resolution");

        // GIF and WEBM are intentionally more permissive than the H.264/HEVC MP4 path:
        //   - GIF has no real bitrate knob (it's lossless indexed palette); the bitrate field
        //     is ignored by the encoder, so validating it would just reject valid presets.
        //   - WEBM/VP9 accepts a wide range of target bitrates and lower frame rates.
        // The strict 30/60 + 500–200000kbps contract only applies to the MP4 family.
        if (Codec == VideoCodec.GIF)
        {
            // GIF animation typically runs at 10–30fps; accept anything reasonable.
            if (FrameRate < 1 || FrameRate > 60)
                throw new InvalidOperationException($"GIF FrameRate out of range: {FrameRate}");
            return;
        }

        if (Codec == VideoCodec.WEBM)
        {
            if (FrameRate != 30 && FrameRate != 60)
                throw new InvalidOperationException($"FrameRate must be 30 or 60, got {FrameRate}");
            if (BitrateKbps < 100 || BitrateKbps > 200_000)
                throw new InvalidOperationException($"BitrateKbps out of range: {BitrateKbps}");
            return;
        }

        // H264 / HEVC — original strict contract.
        if (FrameRate != 30 && FrameRate != 60)
            throw new InvalidOperationException($"FrameRate must be 30 or 60, got {FrameRate}");
        if (BitrateKbps < 500 || BitrateKbps > 200_000)
            throw new InvalidOperationException($"BitrateKbps out of range: {BitrateKbps}");
    }
}
