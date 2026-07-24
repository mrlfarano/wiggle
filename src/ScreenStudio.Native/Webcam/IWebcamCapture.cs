namespace ScreenStudio.Native.Webcam;

/// <summary>A captured webcam frame: timestamped BGRA byte buffer + dimensions. Unlike
/// <c>CapturedFrame</c> (which carries a pinned native texture handle for the GPU path),
/// webcam frames are delivered as plain managed byte arrays — PiP compositing is a CPU
/// operation and copying the bytes up-front avoids a dangling pin if the consumer holds
/// the frame across capture ticks.</summary>
public readonly record struct WebcamFrame(double TimestampMs, byte[] Bgra, int Width, int Height);

/// <summary>Webcam capture options (PRD P1 #3: "Webcam Recording"). The PiP inset is a
/// normalized rectangle in 0..1 output-frame coordinates so it survives output rescaling.</summary>
public sealed class WebcamOptions
{
    /// <summary>Device name from <see cref="IWebcamCapture.ListCameras"/>. If null/empty the
    /// first enumerated device is used.</summary>
    public string? DeviceName { get; set; }
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public int TargetFrameRate { get; set; } = 30;
}

/// <summary>Task 016 — webcam capture for the picture-in-picture overlay. Implementations
/// stream webcam frames as BGRA byte arrays via <see cref="Frames"/>. The ffmpeg/dshow-backed
/// <c>FfmpegWebcamCapture</c> is the default; a future Media Foundation back end could provide
/// hardware-accelerated capture.</summary>
public interface IWebcamCapture : IDisposable
{
    IObservable<WebcamFrame> Frames { get; }

    /// <summary>True when ffmpeg (or the underlying capture API) is present AND at least one
    /// video input device is available. In a headless session dshow enumerates nothing.</summary>
    bool IsAvailable();

    /// <summary>Enumerates the names of available DirectShow video input devices. Empty if
    /// none, or if ffmpeg is absent.</summary>
    IReadOnlyList<string> ListCameras();

    void Start(WebcamOptions options);
    void Stop();
}
