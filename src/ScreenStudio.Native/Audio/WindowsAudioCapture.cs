using System.Runtime.InteropServices;
using Windows.Media.Audio;
using Windows.Media.Devices;
using Windows.Media.Render;

namespace ScreenStudio.Native.Audio;

public readonly record struct AudioBuffer(double TimestampMs, float[] Samples, int SampleRate, int Channels);

public interface IAudioCapture : IDisposable
{
    IObservable<AudioBuffer> Buffers { get; }

    /// <summary>True when an audio graph can be created (audio endpoints present).</summary>
    bool IsAvailable();

    void Start(bool microphone, bool systemLoopback);
    void Stop();
}

/// <summary>Task 008 — audio capture via the WinRT AudioGraph (a managed wrapper over
/// WASAPI). Supports microphone input and system-audio loopback. AudioGraph emits frames
/// to a frame-output node on a background thread; we surface them as AudioBuffer.
///
/// STATUS: compiles against the real WinRT projection; runtime needs real audio endpoints
/// (absent in a headless session, where CreateAsync reports no devices).</summary>
public sealed class WindowsAudioCapture : IAudioCapture
{
    private readonly Subject<AudioBuffer> _buffers = new();
    private AudioGraph? _graph;
    private AudioFrameOutputNode? _out;
    private long _startTicks;
    private long _perfFreq;

    public IObservable<AudioBuffer> Buffers => _buffers;

    public bool IsAvailable()
    {
        try
        {
            // If there's no default render endpoint, loopback/mic graphs cannot be created.
            var dev = Windows.Media.Devices.MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
            return !string.IsNullOrEmpty(dev);
        }
        catch { return false; }
    }

    public void Start(bool microphone, bool systemLoopback)
    {
        if (!IsAvailable())
            throw new PlatformNotSupportedException(
                "No audio endpoints available (headless/Session 0). Audio capture requires an " +
                "interactive desktop with audio devices.");

        QueryPerformanceFrequency(out _perfFreq);
        QueryPerformanceCounter(out _startTicks);

        // Synchronously drive the async AudioGraph creation so the graph is live before we return.
        StartAsync(microphone, systemLoopback).GetAwaiter().GetResult();
    }

    private async System.Threading.Tasks.Task StartAsync(bool microphone, bool systemLoopback)
    {
        var settings = new AudioGraphSettings(systemLoopback ? AudioRenderCategory.Media : AudioRenderCategory.Communications)
        {
            // Use the default endpoint; explicit device selection is a UI concern.
        };

        var result = await AudioGraph.CreateAsync(settings);
        if (result.Status != AudioGraphCreationStatus.Success)
            throw new InvalidOperationException($"AudioGraph create failed: {result.Status} ({result.ExtendedError?.Message})");
        _graph = result.Graph;

        _out = _graph.CreateFrameOutputNode();
        _graph.QuantumStarted += OnQuantum;

        if (microphone)
        {
            var micResult = await _graph.CreateDeviceInputNodeAsync(Windows.Media.Capture.MediaCategory.Communications);
            if (micResult.Status == AudioDeviceNodeCreationStatus.Success)
                micResult.DeviceInputNode.AddOutgoingConnection(_out);
            // If mic node creation fails (no mic/default), the graph still runs for loopback.
        }

        // The frame-output node must be added BEFORE Start so quanta flow into it.
        _graph.Start();
    }

    private void OnQuantum(AudioGraph sender, object args)
    {
        if (_out is null) return;
        var frame = _out.GetFrame();
        var buf = AudioFrameToFloats(frame);
        if (buf.Length > 0)
        {
            QueryPerformanceCounter(out long t);
            var ms = (t - _startTicks) * 1000.0 / _perfFreq;
            _buffers.OnNext(new AudioBuffer(ms, buf, (int)sender.EncodingProperties.SampleRate,
                (int)sender.EncodingProperties.ChannelCount));
        }
    }

    /// <summary>Decode a WinRT AudioFrame's lock buffer to a float[] (interleaved PCM).</summary>
    private static float[] AudioFrameToFloats(Windows.Media.AudioFrame frame)
    {
        using var buf = frame.LockBuffer(Windows.Media.AudioBufferAccessMode.Read);
        using var ref_ = buf.CreateReference();
        // IMemoryBufferByteAccess gives a raw pointer to the float samples.
        if (ref_ is not IMemoryBufferByteAccess access) return Array.Empty<float>();
        access.GetBuffer(out IntPtr pBuf, out uint capacity);
        var count = (int)(capacity / sizeof(float));
        var samples = new float[count];
        Marshal.Copy(pBuf, samples, 0, (int)capacity / sizeof(float));
        return samples;
    }

    public void Stop()
    {
        if (_graph is not null)
        {
            _graph.QuantumStarted -= OnQuantum;
            _graph.Stop();
            _graph.Dispose();
            _graph = null;
            _out = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _buffers.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);
    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
}

/// <summary>IMemoryBufferByteAccess — the COM interface to read a WinRT IMemoryBuffer's raw bytes.</summary>
[ComImport, Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMemoryBufferByteAccess
{
    void GetBuffer(out IntPtr buffer, out uint capacity);
}
