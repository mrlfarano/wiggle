using System.Runtime.InteropServices;
using ScreenStudio.Core.Export;

namespace ScreenStudio.Native.Encoding;

/// <summary>Task 006 — H.264/HEVC encoder via the Media Foundation sink writer.
/// Accepts RGB32 (BGRA) frame buffers and writes an MP4 file. Concrete implementation
/// of <see cref="IVideoEncoder"/>; supersedes the NullVideoEncoder stub.
///
/// On any Windows 10/11 desktop (MF always present) it produces a valid, playable MP4.
/// Flow: configure output media type + input media type, BeginWriting, WriteSample per
/// frame (one IMFMediaBuffer per frame), DoFinalize.</summary>
public sealed class MediaFoundationEncoder : IVideoEncoder
{
    private const int HNS_PER_SECOND = 10_000_000; // hundred-nanosecond units

    /// <summary>True when Media Foundation is truly functional on this machine (not just
    /// present). On Windows N-skus / some container/headless Windows images, mfplat.dll
    /// exists but its objects don't implement MF interfaces (QI for IMFAttributes fails).
    /// We probe once and cache.</summary>
    public static bool IsAvailable()
    {
        if (_availabilityKnown) return _available;
        _availabilityKnown = true;
        try
        {
            if (MediaFoundationInterop.MFStartup(MediaFoundationInterop.MF_VERSION, MediaFoundationInterop.MFSTARTUP_LITE) < 0)
                return _available = false;

            // MFCreateAttributes returns an IUnknown* via raw marshalling (avoids the cast
            // that would itself throw E_NOINTERFACE in a stub environment).
            int hr = MFCreateAttributesRaw(out IntPtr pUnk, 1);
            if (hr < 0 || pUnk == IntPtr.Zero) { MediaFoundationInterop.MFShutdown(); return _available = false; }
            try
            {
                var iid = typeof(IMFAttributes).GUID;
                hr = Marshal.QueryInterface(pUnk, ref iid, out IntPtr pItf);
                if (pItf != IntPtr.Zero) Marshal.Release(pItf);
                _available = hr >= 0;
            }
            finally { Marshal.Release(pUnk); MediaFoundationInterop.MFShutdown(); }
            return _available;
        }
        catch { return _available = false; }
    }

    [DllImport("mfplat.dll", EntryPoint = "MFCreateAttributes")]
    private static extern int MFCreateAttributesRaw(out IntPtr ppMFAttributes, int cInitialSize);

    private static bool _availabilityKnown;
    private static bool _available;

    private IMFSinkWriter? _writer;
    private int _streamIndex;
    private int _width;
    private int _height;
    private int _fps;
    private long _frameDurationTicks;
    private long _currentTicks;
    private bool _started;
    private bool _finalized;
    private bool _ownsStartup;

    public void Initialize(ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        if (!IsAvailable())
            throw new PlatformNotSupportedException(
                "Media Foundation is not functional on this machine (Windows N-edition without the " +
                "Media Feature Pack, or a container/headless Windows image with a stub mfplat.dll). " +
                "H.264 encoding requires a desktop Windows with the media stack installed.");

        _width = settings.Resolution.Width;
        _height = settings.Resolution.Height;
        _fps = settings.FrameRate;
        _frameDurationTicks = HNS_PER_SECOND / _fps;
        _currentTicks = 0;
        _started = false;
        _finalized = false;

        int hr = MediaFoundationInterop.MFStartup(MediaFoundationInterop.MF_VERSION, MediaFoundationInterop.MFSTARTUP_LITE);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        _ownsStartup = true;

        // Container attribute (MP4).
        MediaFoundationInterop.MFCreateAttributes(out var containerAttrs, 1);
        var containerType = MediaFoundationInterop.MFTranscodeContainerType_MP4;
        containerAttrs.SetGUID(MediaFoundationInterop.MF_TRANSCODE_CONTAINERTYPE, ref containerType);
        // Cast-encode the attribute set pointer for the factory call.
        var attrsPtr = Marshal.GetIUnknownForObject(containerAttrs);

        try
        {
            hr = MediaFoundationInterop.MFCreateSinkWriterFromURL(settings.OutputPath, attrsPtr, IntPtr.Zero, out _writer);
        }
        finally { Marshal.Release(attrsPtr); }
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;

        ConfigureOutputAndInput(settings);
    }

    private void ConfigureOutputAndInput(ExportSettings settings)
    {
        var codec = settings.Codec == VideoCodec.HEVC
            ? MediaFoundationInterop.MFVideoFormat_HEVC
            : MediaFoundationInterop.MFVideoFormat_H264;
        var majorVideo = MediaFoundationInterop.MFMediaType_Video;

        // --- Output media type (encoded H.264/HEVC in MP4) ---
        int hr = MediaFoundationInterop.MFCreateMediaType(out var outMt);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        outMt.SetGUID(MediaFoundationInterop.MF_MT_MAJOR_TYPE, ref majorVideo);
        outMt.SetGUID(MediaFoundationInterop.MF_MT_SUBTYPE, ref codec);
        outMt.SetUINT32(MediaFoundationInterop.MF_MT_INTERLACE_MODE, 2); // progressive
        outMt.SetUINT64(MediaFoundationInterop.MF_MT_FRAME_SIZE, Pack(_width, _height));
        outMt.SetUINT64(MediaFoundationInterop.MF_MT_FRAME_RATE, Pack(_fps, 1));
        outMt.SetUINT32(MediaFoundationInterop.MF_MT_AVG_BITRATE, (uint)settings.BitrateKbps * 1000u);

        hr = _writer!.AddStream(outMt, out _streamIndex);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;

        // --- Input media type (RGB32) ---
        hr = MediaFoundationInterop.MFCreateMediaType(out var inMt);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        var rgb32 = MediaFoundationInterop.MFVideoFormat_RGB32;
        inMt.SetGUID(MediaFoundationInterop.MF_MT_MAJOR_TYPE, ref majorVideo);
        inMt.SetGUID(MediaFoundationInterop.MF_MT_SUBTYPE, ref rgb32);
        inMt.SetUINT32(MediaFoundationInterop.MF_MT_INTERLACE_MODE, 2);
        inMt.SetUINT64(MediaFoundationInterop.MF_MT_FRAME_SIZE, Pack(_width, _height));
        inMt.SetUINT64(MediaFoundationInterop.MF_MT_FRAME_RATE, Pack(_fps, 1));

        hr = _writer.SetInputMediaType(_streamIndex, inMt, IntPtr.Zero);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
    }

    /// <summary>Push one RGB32 frame. <paramref name="rgb32"/> must contain width*height*4 bytes.</summary>
    public void WriteFrameRgb32(byte[] rgb32)
    {
        ObjectDisposedException.ThrowIf(_writer is null, this);
        var stride = _width * 4;
        var needed = _height * stride;
        if (rgb32.Length < needed)
            throw new ArgumentException($"frame buffer too small: {rgb32.Length} < {needed}");

        EnsureStarted();

        int hr = MediaFoundationInterop.MFCreateMemoryBuffer(needed, out var buffer);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;

        hr = buffer.Lock(out IntPtr pBuf, out _, out _);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        Marshal.Copy(rgb32, 0, pBuf, needed);
        buffer.Unlock();
        buffer.SetCurrentLength(needed);

        hr = MediaFoundationInterop.MFCreateSample(out var sample);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        sample.AddBuffer(buffer);
        sample.SetSampleTime(_currentTicks);
        sample.SetSampleDuration(_frameDurationTicks);

        hr = _writer!.WriteSample(_streamIndex, sample);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;

        _currentTicks += _frameDurationTicks;
    }

    // IVideoEncoder.WriteFrame (texture path) — not yet implemented; callers use WriteFrameRgb32.
    void IVideoEncoder.WriteFrame(ComposeFrame frame, IntPtr d3dTexture)
        => throw new NotSupportedException("Texture path not wired; use WriteFrameRgb32 with a BGRA buffer.");

    private void EnsureStarted()
    {
        if (_started) return;
        int hr = _writer!.BeginWriting();
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        _started = true;
    }

    public void FinalizeStream()
    {
        if (_finalized) return;
        _finalized = true;
        if (_writer is not null)
        {
            int hr = _writer.DoFinalize();
            _writer = null;
            if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        }
        if (_ownsStartup) { MediaFoundationInterop.MFShutdown(); _ownsStartup = false; }
    }

    public void Dispose() => FinalizeStream();

    // MF packs two UINT32 into one UINT64.
    private static ulong Pack(int high, int low) => ((ulong)(uint)high << 32) | (uint)low;
}
