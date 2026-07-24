using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using ScreenStudio.Native;

namespace ScreenStudio.Native.Capture;

/// <summary>Task 002 — screen capture backed by FFmpeg (gdigrab on Windows, x11grab on Linux).
/// Produces real captured frames as BGRA byte buffers. An alternative to the WGC-based
/// <see cref="WindowsScreenCapture"/> for environments without a usable GPU/DXGI adapter
/// (gdigrab uses GDI, which works without a 3D device).
///
/// Frames arrive on a background reader thread and are surfaced via <see cref="Frames"/>.</summary>
public sealed class FfmpegScreenCapture : IScreenCapture
{
    private readonly Subject<CapturedFrame> _frames = new();
    private readonly Subject<CursorEvent> _cursor = new();
    private Process? _process;
    private Stream? _stdout;
    private Thread? _readThread;
    private volatile bool _running;
    private long _perfFreq;
    private long _startTicks;
    private int _width;
    private int _height;

    public IObservable<CapturedFrame> Frames => _frames;
    public IObservable<CursorEvent> CursorEvents => _cursor;

    public static bool IsAvailableStatic() => Encoding.FfmpegEncoder.FindFfmpeg() != null;

    public bool IsAvailable() => IsAvailableStatic();

    public void Start(CaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var exe = Encoding.FfmpegEncoder.FindFfmpeg()
            ?? throw new PlatformNotSupportedException("ffmpeg not found; cannot capture screen.");

        _width = options.RegionSize.X > 0 ? (int)options.RegionSize.X : 1920;
        _height = options.RegionSize.Y > 0 ? (int)options.RegionSize.Y : 1080;
        var fps = options.TargetFrameRate > 0 ? options.TargetFrameRate : 30;

        QueryPerformanceFrequency(out _perfFreq);
        QueryPerformanceCounter(out _startTicks);

        // gdigrab captures the Windows desktop via GDI (no GPU/DXGI required).
        var args = $"-y -loglevel error -f gdigrab -framerate {fps} -i desktop " +
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
            throw new InvalidOperationException("Failed to start ffmpeg screen capture.");
        _stdout = _process.StandardOutput.BaseStream;
        _running = true;

        // Drain stderr to avoid the redirected-process deadlock.
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try { while (!_process.StandardError.EndOfStream) _process.StandardError.ReadLine(); }
            catch { }
        });

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ffmpeg-screen-reader" };
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
            // Pin the frame so the texture handle is stable for the consumer's lifetime.
            var copy = (byte[])buf.Clone();
            var handle = GCHandle.Alloc(copy, GCHandleType.Pinned);
            _frames.OnNext(new CapturedFrame(ms, handle.AddrOfPinnedObject(), _width, _height));
            // The consumer must copy what it needs immediately; we free the pin after dispatch.
            handle.Free();
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
        _cursor.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
}
