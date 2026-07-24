using System.Runtime.InteropServices;

namespace ScreenStudio.Native.Encoding;

/// <summary>Minimal, correct Media Foundation COM interop for an H.264 MP4 sink writer.
/// Only the surface used by <see cref="MediaFoundationEncoder"/> is declared.
/// GUIDs are the documented constants from mfapi.h / mfreadwrite.h.</summary>
internal static class MediaFoundationInterop
{
    public const int MF_VERSION = (2 << 16) | 0x0;
    public const int MFSTARTUP_LITE = 0x1;

    // Major type / subtypes
    public static readonly Guid MFMediaType_Video            = new("72178C23-F45D-45C3-ADB5-B82D654B7631");
    public static readonly Guid MFVideoFormat_RGB32          = new("00000016-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_H264           = new("33363248-0000-0010-8000-00AA00389B71"); // "H264"
    public static readonly Guid MFVideoFormat_HEVC           = new("43564548-0000-0010-8000-00AA00389B71"); // "HEVC"
    public static readonly Guid MFTranscodeContainerType_MP4 = new("4BC53F4A-6D4D-4B5D-A82C-4E42A0C45EA8");
    public static readonly Guid MF_TRANSCODE_CONTAINERTYPE = new("150B3EBA-3C3D-4F3A-9D1F-B38A8A2DC05B");

    // Attribute keys
    public static readonly Guid MF_MT_MAJOR_TYPE             = new("48EBA18E-F8C9-488C-B11C-EA51B81E0A33");
    public static readonly Guid MF_MT_SUBTYPE                = new("F7E34C9A-46E1-4F9C-AE9C-B98F12AC4B7A");
    public static readonly Guid MF_MT_FRAME_SIZE             = new("1652C33D-D6B2-4012-B834-72049261611A");
    public static readonly Guid MF_MT_FRAME_RATE             = new("C459A2E4-0062-4A4B-9CA1-8B7B4C51AD00");
    public static readonly Guid MF_MT_INTERLACE_MODE         = new("E27B85A7-211A-4B9D-A76A-44D76D5B9C3D");
    public static readonly Guid MF_MT_AVG_BITRATE            = new("5731EA11-006C-444D-9CA2-8F39400C3C00");
    public static readonly Guid MF_MT_ALL_SAMPLES_INDEPENDENT = new("C9173739-5E56-4F94-811A-17A78A4B7B40");

    [DllImport("mfplat.dll")]
    public static extern int MFStartup(int Version, int dwFlags);

    [DllImport("mfplat.dll")]
    public static extern int MFShutdown();

    [DllImport("mfplat.dll")]
    public static extern int MFCreateAttributes(out IMFAttributes ppMFAttributes, int cInitialSize);

    [DllImport("mfplat.dll")]
    public static extern int MFCreateMediaType(out IMFMediaType ppMFMediaType);

    [DllImport("mfplat.dll")]
    public static extern int MFCreateMemoryBuffer(int cbMaxLength, out IMFMediaBuffer ppBuffer);

    [DllImport("mfplat.dll")]
    public static extern int MFCreateSample(out IMFSample ppIMFSample);

    [DllImport("mf.dll", CharSet = CharSet.Unicode)]
    public static extern int MFCreateSinkWriterFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string pszOutputURL,
        IntPtr pAttributes,
        IntPtr pAttributes2,
        out IMFSinkWriter ppSinkWriter);
}
