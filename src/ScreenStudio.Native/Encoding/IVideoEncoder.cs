using ScreenStudio.Core.Export;

namespace ScreenStudio.Native.Encoding;

/// <summary>Contract for a video encoder backing the export pipeline (task 006).
/// Implementations: <see cref="MediaFoundationEncoder"/> (real, H.264/HEVC MP4),
/// <see cref="NullVideoEncoder"/> (no-op, for environments without Media Foundation).</summary>
public interface IVideoEncoder : IDisposable
{
    /// <summary>Open the sink writer for the configured output path/container.</summary>
    void Initialize(ExportSettings settings);

    /// <summary>Push one composed frame (GPU texture) to the encoder.</summary>
    void WriteFrame(ComposeFrame frame, IntPtr d3dTexture);

    /// <summary>Finalize the container and close the file.</summary>
    void FinalizeStream();
}
