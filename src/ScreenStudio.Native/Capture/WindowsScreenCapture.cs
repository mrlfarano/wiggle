using System.Runtime.InteropServices;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenStudio.Native.Capture;

/// <summary>What to capture (PRD P0: "Record full screen or selected area").</summary>
public enum CaptureTarget { FullScreen, Display, Window, Region }

public sealed class CaptureOptions
{
    public CaptureTarget Target { get; set; } = CaptureTarget.FullScreen;
    /// <summary>For Region/Window targets.</summary>
    public int? TargetHandle { get; set; }
    public Vec2 RegionTopLeft { get; set; }
    public Vec2 RegionSize { get; set; }
    public int TargetFrameRate { get; set; } = 60;
    public bool CaptureCursor { get; set; } = true;     // WGC can include the system cursor
    public bool CaptureSystemAudio { get; set; } = true; // WASAPI loopback
    public bool CaptureMicrophone { get; set; } = true;
}

/// <summary>A captured frame: timestamped D3D11 texture handle (opaque IDirect3DSurface
/// wrapper) + dimensions. The WinRT Direct3D11CaptureFrame's surface is exposed here.</summary>
public readonly record struct CapturedFrame(
    double TimestampMs,
    IntPtr TextureHandle,
    int Width,
    int Height);

/// <summary>Live cursor events from the WH_MOUSE_LL hook + GetCursorInfo polling,
/// timestamped with QueryPerformanceCounter ticks converted to ms.</summary>
public interface IScreenCapture : IDisposable
{
    IObservable<CapturedFrame> Frames { get; }
    IObservable<CursorEvent> CursorEvents { get; }

    /// <summary>True when WGC can actually run here. WGC requires a graphical session
    /// (an interactive desktop with at least one monitor). In a headless/service context
    /// it cannot enumerate GraphicsCaptureItems and Start() throws.</summary>
    bool IsAvailable();

    void Start(CaptureOptions options);
    void Stop();
}

/// <summary>Task 002 — Windows Graphics Capture (WGC) backed screen capture.
///
/// Real implementation against the WinRT projection. WGC pipeline:
///   1. Create a Direct3D11Device (we create a D3D11.0 device via DXGI).
///   2. Wrap it as WinRT IDirect3DDevice (CreateDirect3D11DeviceFromDXGIDevice).
///   3. Build a GraphicsCaptureItem (CreateFromMonitor for full-screen, CreateFromWindow for a window).
///   4. Create a Direct3D11CaptureFramePool + GraphicsCaptureSession, Start().
///   5. On FrameArrived, surface the IDirect3DSurface as CapturedFrame.
///
/// Note: GraphicsCaptureItem.CreateFromMonitor/CreateFromWindow are factory methods exposed
/// via the IGraphicsCaptureItemInterop COM interface (create-on-interop), not directly on the
/// projected type. We obtain them through the interop cast below.</summary>
public sealed class WindowsScreenCapture : IScreenCapture
{
    private readonly Subject<CapturedFrame> _frames = new();
    private readonly Subject<CursorEvent> _cursor = new();
    private readonly CursorHook _cursorHook = new();
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private GraphicsCaptureItem? _item;
    private long _perfFreq;
    private long _startTicks;

    public IObservable<CapturedFrame> Frames => _frames;
    public IObservable<CursorEvent> CursorEvents => _cursor;

    public bool IsAvailable()
    {
        // WGC's GraphicsCaptureSession.IsSupported() reports whether capture can run in
        // the current session (false on headless/Sessions 0 and server-core).
        try { return GraphicsCaptureSession.IsSupported(); }
        catch { return false; }
    }

    public void Start(CaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!IsAvailable())
            throw new PlatformNotSupportedException(
                "Windows Graphics Capture is not available in this session (requires an interactive " +
                "desktop; cannot run headless or in Session 0).");

        QueryPerformanceFrequency(out _perfFreq);
        _startTicks = 0;
        QueryPerformanceCounter(out long now);
        _startTicks = now;

        // 1) D3D11 device + WinRT wrap. In a session without a usable GPU (containers,
        // Session 0, RDP without GPU), D3D11CreateDevice returns E_INVALIDARG — there is no
        // DXGI adapter to target. Surface that as a clear unavailability rather than a raw
        // ArgumentException.
        IntPtr dxgiDevice;
        try
        {
            dxgiDevice = Direct3D11Helper.CreateD3D11Device(out var d3dDevice);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("expected range"))
        {
            throw new PlatformNotSupportedException(
                "No usable DirectX/GPU device in this session. WGC capture requires a graphics " +
                "adapter (not available in containers or headless sessions).", ex);
        }
        var winRtDevice = Direct3D11Helper.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice);

        // 2) Build the capture item.
        _item = options.Target == CaptureTarget.Window && options.TargetHandle.HasValue
            ? GraphicsCaptureItemInterop.CreateFromWindow(options.TargetHandle.Value)
            : GraphicsCaptureItemInterop.CreateFromPrimaryMonitor();

        // 3) Frame pool + session.
        var format = DirectXPixelFormat.B8G8R8A8UIntNormalized;
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winRtDevice, format, 2, _item.Size);
        _framePool.FrameArrived += OnFrameArrived;

        _session = _framePool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = options.CaptureCursor;
        StartSession(_session);

        if (options.CaptureCursor)
            _cursorHook.Start(ToMs, _cursor);
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;
        var surface = frame.Surface;
        var ptr = Marshal.GetIUnknownForObject(surface);
        _frames.OnNext(new CapturedFrame(NowMs(), ptr, frame.ContentSize.Width, frame.ContentSize.Height));
        Marshal.Release(ptr);
    }

    private double ToMs(long ticks) => _perfFreq > 0 ? ticks * 1000.0 / _perfFreq : 0;
    private double NowMs()
    {
        QueryPerformanceCounter(out long t);
        return (t - _startTicks) * 1000.0 / _perfFreq;
    }

    public void Stop()
    {
        _cursorHook.Stop();
        _session?.Dispose();
        _framePool?.Dispose();
        _session = null;
        _framePool = null;
        _item = null;
    }

    public void Dispose()
    {
        Stop();
        _frames.Dispose();
        _cursor.Dispose();
        _cursorHook.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);
    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    /// <summary>Start a WGC session. In the 19041 contract the projected method is
    /// StartCapture (fire-and-forget). Begin capture on the session.</summary>
    private static void StartSession(GraphicsCaptureSession session)
        => session.StartCapture();
}
