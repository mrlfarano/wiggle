using System.Diagnostics;
using ScreenStudio.Native;

namespace ScreenStudio.Native.Webcam;

/// <summary>Task 016 — webcam capture backed by FFmpeg's DirectShow input. Produces real
/// webcam frames as BGRA byte buffers, streamed from ffmpeg's stdout, mirroring the
/// <see cref="Capture.FfmpegScreenCapture"/> / <see cref="Audio.FfmpegAudioCapture"/>
/// architecture (background reader thread + internal <see cref="Subject{T}"/>).
///
/// ffmpeg dshow pipeline:
///   <c>ffmpeg -f dshow -i video="Camera Name" -pix_fmt bgra -f rawvideo -</c>
/// frames arrive row-major top-down at the requested resolution; we surface them as
/// <see cref="WebcamFrame"/> for the CPU PiP compositor in
/// <see cref="Core.Export.FrameCompositor"/>.
///
/// STATUS: compiles and enumerates real devices via dshow; runtime capture needs a real
/// webcam (absent in headless CI, where <see cref="ListCameras"/> returns empty and Start()
/// throws <see cref="PlatformNotSupportedException"/>).</summary>
public sealed class FfmpegWebcamCapture : IWebcamCapture
{
    private readonly Subject<WebcamFrame> _frames = new();
    private Process? _process;
    private Stream? _stdout;
    private Thread? _readThread;
    private volatile bool _running;
    private long _perfFreq;
    private long _startTicks;
    private int _width;
    private int _height;

    public IObservable<WebcamFrame> Frames => _frames;

    public static bool IsAvailableStatic() => Encoding.FfmpegEncoder.FindFfmpeg() != null;

    /// <summary>Enumerates DirectShow video input devices via
    /// <c>ffmpeg -f dshow -list_devices true -i video=</c>. Empty if ffmpeg is absent or no
    /// cameras are connected. Matches the parsing shape of
    /// <see cref="Audio.FfmpegAudioCapture.ListMicrophones"/> (quoted "Name" + "(video)" tag).</summary>
    public IReadOnlyList<string> ListCameras()
    {
        var names = new List<string>();
        var exe = Encoding.FfmpegEncoder.FindFfmpeg();
        if (exe == null) return names;
        try
        {
            var psi = new ProcessStartInfo(exe, "-hide_banner -f dshow -list_devices true -i video=")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return names;
            // dshow device enumeration is written to stderr (the -i video= input has no
            // container; ffmpeg exits non-zero after listing). Read until the pipe closes.
            while (!p.StandardError.EndOfStream)
            {
                var line = p.StandardError.ReadLine() ?? "";
                // dshow lists: [dshow @ ...] "Device Name" (video)
                var idx = line.IndexOf('"');
                var idx2 = idx >= 0 ? line.IndexOf('"', idx + 1) : -1;
                if (idx >= 0 && idx2 > idx && line.Contains("(video)"))
                    names.Add(line.Substring(idx + 1, idx2 - idx - 1));
            }
            p.WaitForExit(3000);
        }
        catch { /* device enumeration is best-effort; absence is reported as empty */ }
        return names;
    }

    public bool IsAvailable() => ListCameras().Count > 0;

    public void Start(WebcamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var exe = Encoding.FfmpegEncoder.FindFfmpeg()
            ?? throw new PlatformNotSupportedException("ffmpeg not found; cannot capture webcam.");

        var device = !string.IsNullOrWhiteSpace(options.DeviceName)
            ? options.DeviceName
            : ListCameras().FirstOrDefault();
        if (string.IsNullOrEmpty(device))
            throw new PlatformNotSupportedException("No webcam devices available.");

        _width = options.Width > 0 ? options.Width : 640;
        _height = options.Height > 0 ? options.Height : 480;
        var fps = options.TargetFrameRate > 0 ? options.TargetFrameRate : 30;

        QueryPerformanceFrequency(out _perfFreq);
        QueryPerformanceCounter(out _startTicks);

        // Quote the device name: dshow names contain spaces/parens and must reach ffmpeg as a
        // single argument token. Request an exact frame size so each stdout read is one frame.
        // -pix_fmt bgra gives row-major top-down BGRA matching FrameCompositor's expectation.
        var args =
            $"-y -loglevel error -f dshow -framerate {fps} -i video=\"{device}\" " +
            $"-s {_width}x{_height} -pix_fmt bgra -f rawvideo -";

        _process = new Process
        {
            StartInfo = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        if (!_process.Start())
            throw new InvalidOperationException("Failed to start ffmpeg webcam capture.");
        _stdout = _process.StandardOutput.BaseStream;
        _running = true;

        // Drain stderr on a background thread — otherwise ffmpeg can deadlock filling the
        // stderr pipe buffer while we read stdout (classic redirected-process deadlock).
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try { while (!_process.StandardError.EndOfStream) _process.StandardError.ReadLine(); }
            catch { }
        });

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ffmpeg-webcam-reader" };
        _readThread.Start();
    }

    private void ReadLoop()
    {
        var frameBytes = _width * _height * 4;
        var buf = new byte[frameBytes];
        while (_running)
        {
            int read = ReadExact(_stdout!, buf);
            if (read < frameBytes) break;
            QueryPerformanceCounter(out long t);
            var ms = (t - _startTicks) * 1000.0 / _perfFreq;
            // Copy before dispatch: the consumer (PiP compositor) may hold the frame across
            // the next capture tick, so we never hand out our scratch buffer.
            var copy = (byte[])buf.Clone();
            _frames.OnNext(new WebcamFrame(ms, copy, _width, _height));
        }
    }

    private static int ReadExact(Stream s, byte[] buf)
    {
        int total = 0;
        while (total < buf.Length)
        {
            int n = s.Read(buf, total, buf.Length - total);
            if (n <= 0) break;
            total += n;
        }
        return total;
    }

    public void Stop()
    {
        _running = false;
        try { _process?.Kill(); } catch { }
        _readThread?.Join(1000);
        _process = null;
        _stdout = null;
    }

    public void Dispose()
    {
        Stop();
        _frames.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
}
