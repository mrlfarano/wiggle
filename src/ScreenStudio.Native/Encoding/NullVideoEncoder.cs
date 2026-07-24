using ScreenStudio.Core.Export;

namespace ScreenStudio.Native.Encoding;

/// <summary>No-op encoder, used when Media Foundation is unavailable (e.g. non-Windows,
/// N-Edition SKUs without the Media Feature Pack, or headless containers). Writes nothing.</summary>
public sealed class NullVideoEncoder : IVideoEncoder
{
    public void Initialize(ExportSettings settings) { }
    public void WriteFrame(ComposeFrame frame, IntPtr d3dTexture) { }
    public void FinalizeStream() { }
    public void Dispose() { }
}
