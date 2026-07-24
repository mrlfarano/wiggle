using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenStudio.Core.Export;

namespace ScreenStudio.Native.Encoding;

/// <summary>Task 006 — H.264 encoder backed by FFmpeg (libx264). Produces a REAL MP4 file.
///
/// This is an alternative to <see cref="MediaFoundationEncoder"/> for environments where the
/// Media Foundation COM object model is non-functional (e.g. containerized Windows with a stub
/// mfplat.dll). FFmpeg's libx264 is a fully featured H.264 encoder; feeding raw BGRA frames on
/// stdin via the rawvideo demuxer yields a standards-compliant MP4.
///
/// Feed frames with <see cref="WriteFrameRgb32"/>; call <see cref="FinalizeStream"/> to close
/// the pipe and let ffmpeg finalize the container.</summary>
public sealed class FfmpegEncoder : IVideoEncoder
{
    private Process? _process;
    private Stream? _stdin;
    private ExportSettings? _settings;
    private int _width;
    private int _height;
    private int _fps;

    /// <summary>Locate the ffmpeg executable. Searches PATH and common install locations.</summary>
    public static string? FindFfmpeg()
    {
        var name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        // PATH lookup via where/which
        try
        {
            var psi = new ProcessStartInfo(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which", name)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var line = p.StandardOutput.ReadLine();
                p.WaitForExit(2000);
                if (!string.IsNullOrWhiteSpace(line) && File.Exists(line)) return line.Trim();
            }
        }
        catch { /* ignore, fall through to known paths */ }

        string[] known = {
            @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            "/usr/bin/ffmpeg",
            "/usr/local/bin/ffmpeg",
        };
        foreach (var k in known) if (File.Exists(k)) return k;
        return null;
    }

    public static bool IsAvailable() => FindFfmpeg() != null;

    public void Initialize(ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var exe = FindFfmpeg()
            ?? throw new PlatformNotSupportedException("ffmpeg not found on PATH or known locations.");
        _settings = settings;
        _width = settings.Resolution.Width;
        _height = settings.Resolution.Height;
        _fps = settings.FrameRate;

        // rawvideo BGRA frames are piped to ffmpeg on stdin; the codec decides the rest of
        // the pipeline. -y overwrites; -loglevel error keeps output clean.
        var args = "-y -loglevel error " +
                   $"-f rawvideo -pix_fmt bgra -s {_width}x{_height} -r {_fps} -i - " +
                   BuildCodecArgs(settings);

        _process = new Process
        {
            StartInfo = new ProcessStartInfo(exe, args)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        if (!_process.Start())
            throw new InvalidOperationException("Failed to start ffmpeg.");
        _stdin = _process.StandardInput.BaseStream;
    }

    /// <summary>Builds the output-side (filter + codec + muxer) fragment of the ffmpeg
    /// argument string for the configured <see cref="VideoCodec"/>. Split out so each
    /// container/codec combo is readable and independently testable.
    ///
    /// MP4 family (H.264/HEVC) is unchanged from task 006. GIF and WEBM were added in
    /// task 020 (PRD P2 #4: Advanced Export).</summary>
    private static string BuildCodecArgs(ExportSettings s)
    {
        var w = s.Resolution.Width;
        var h = s.Resolution.Height;
        var path = s.OutputPath;
        switch (s.Codec)
        {
            case VideoCodec.HEVC:
                return $"-c:v libx265 -pix_fmt yuv420p -b:v {s.BitrateKbps}k " +
                       $"-movflags +faststart \"{path}\"";

            case VideoCodec.GIF:
                // Two-pass palette pipeline gives far better GIF quality than the plain gif
                // encoder: palettegen derives an optimal 256-color palette from the input,
                // paletteuse dithers each frame against it. -loop 0 = infinite loop, and we
                // also force a sensible fps so animations don't balloon in size. The palette
                // stats filter runs entirely inside this single ffmpeg invocation.
                return $"-vf \"fps={Math.Clamp(s.FrameRate,1,30)},scale={w}:{h}:flags=lanczos," +
                       $"split[s0][s1];[s0]palettegen=max_colors=256[p];" +
                       $"[s1][p]paletteuse=dither=bayer:bayer_scale=5\" " +
                       $"-loop 0 \"{path}\"";

            case VideoCodec.WEBM:
                // VP9 in a WebM container. -b:v is the target bitrate; -deadline good is the
                // usual speed/quality tradeoff. CRF is intentionally not set so the explicit
                // bitrate from settings governs (matches the H.264 path's behavior).
                return $"-c:v libvpx-vp9 -b:v {s.BitrateKbps}k -pix_fmt yuv420p " +
                       $"-deadline good -cpu-used 4 \"{path}\"";

            case VideoCodec.H264:
            default:
                return $"-c:v libx264 -pix_fmt yuv420p -b:v {s.BitrateKbps}k " +
                       $"-movflags +faststart \"{path}\"";
        }
    }

    public void WriteFrameRgb32(byte[] rgb32)
    {
        ObjectDisposedException.ThrowIf(_stdin is null, this);
        var needed = _width * _height * 4;
        if (rgb32.Length < needed)
            throw new ArgumentException($"frame buffer too small: {rgb32.Length} < {needed}");
        _stdin!.Write(rgb32, 0, needed);
    }

    void IVideoEncoder.WriteFrame(ComposeFrame frame, IntPtr d3dTexture)
        => throw new NotSupportedException("Texture path not wired; use WriteFrameRgb32 with a BGRA buffer.");

    public void FinalizeStream()
    {
        if (_stdin is null) return;
        _stdin.Flush();
        _stdin.Close();
        _stdin = null;
        _process?.WaitForExit(30_000);
        _process = null;
    }

    public void Dispose() => FinalizeStream();
}
