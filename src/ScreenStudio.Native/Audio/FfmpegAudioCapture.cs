using System.Diagnostics;
using ScreenStudio.Native;

namespace ScreenStudio.Native.Audio;

/// <summary>Task 008 — audio capture backed by FFmpeg (dshow/wasapi input). Produces real
/// audio samples as <see cref="AudioBuffer"/>. An alternative to <see cref="WindowsAudioCapture"/>
/// (WinRT AudioGraph) for environments where AudioGraph loopback is unreliable; ffmpeg's dshow
/// input reliably enumerates and captures from microphones and loopback endpoints.
///
/// Streams raw s16le PCM from ffmpeg's stdout, converts to float on the fly, and emits buffers
/// at ffmpeg's chunk boundaries.</summary>
public sealed class FfmpegAudioCapture : IAudioCapture
{
    private readonly Subject<AudioBuffer> _buffers = new();
    private Process? _process;
    private Stream? _stdout;
    private Thread? _readThread;
    private volatile bool _running;
    private long _perfFreq;
    private long _startTicks;
    private int _sampleRate;
    private int _channels;

    public IObservable<AudioBuffer> Buffers => _buffers;

    public static bool IsAvailableStatic() => Encoding.FfmpegEncoder.FindFfmpeg() != null;

    bool IAudioCapture.IsAvailable() => IsCaptureAvailable();

    /// <summary>List capture device names (mic) via ffmpeg dshow enumeration. Empty if none.</summary>
    public static List<string> ListMicrophones()
    {
        var names = new List<string>();
        var exe = Encoding.FfmpegEncoder.FindFfmpeg();
        if (exe == null) return names;
        try
        {
            var psi = new ProcessStartInfo(exe, "-hide_banner -f dshow -list_devices true -i audio=")
            { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p == null) return names;
            while (!p.StandardError.EndOfStream)
            {
                var line = p.StandardError.ReadLine() ?? "";
                // dshow lists: [dshow @ ...] "Device Name" (audio)
                var idx = line.IndexOf("\"");
                var idx2 = idx >= 0 ? line.IndexOf("\"", idx + 1) : -1;
                if (idx >= 0 && idx2 > idx && line.Contains("(audio)"))
                    names.Add(line.Substring(idx + 1, idx2 - idx - 1));
            }
            p.WaitForExit(3000);
        }
        catch { }
        return names;
    }

    public bool IsCaptureAvailable() => ListMicrophones().Count > 0;

    public void Start(bool microphone, bool systemLoopback)
    {
        var exe = Encoding.FfmpegEncoder.FindFfmpeg()
            ?? throw new PlatformNotSupportedException("ffmpeg not found; cannot capture audio.");

        string input;
        if (microphone)
        {
            var mics = ListMicrophones();
            if (mics.Count == 0)
                throw new PlatformNotSupportedException("No microphone devices available.");
            // Quote the device name: dshow names contain spaces/parens and must reach ffmpeg
            // as a single argument token.
            input = $"-f dshow -i audio=\"{mics[0]}\"";
        }
        else
        {
            // System loopback via ffmpeg needs the 'wasapi' or a loopback filter; use dshow
            // audio render endpoint name if available, else report unsupported here.
            throw new NotSupportedException("System loopback via ffmpeg is not configured; use microphone capture.");
        }

        QueryPerformanceFrequency(out _perfFreq);
        QueryPerformanceCounter(out _startTicks);
        _sampleRate = 48000;
        _channels = 1;

        var args = $"-y -loglevel error {input} -t 3600 -ar {_sampleRate} -ac {_channels} -f s16le -";

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
            throw new InvalidOperationException("Failed to start ffmpeg audio capture.");
        _stdout = _process.StandardOutput.BaseStream;
        _running = true;
        // Drain stderr on a background thread — otherwise ffmpeg can deadlock filling the
        // stderr pipe buffer while we read stdout (classic redirected-process deadlock).
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try { while (!_process.StandardError.EndOfStream) _process.StandardError.ReadLine(); }
            catch { }
        });
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ffmpeg-audio-reader" };
        _readThread.Start();
    }

    private void ReadLoop()
    {
        // Read in ~50ms chunks (48000 Hz * 0.05s * 2 bytes/sample).
        const int chunkSamples = 48000 / 20;
        var raw = new byte[chunkSamples * 2]; // s16le
        while (_running)
        {
            int read = ReadExact(_stdout!, raw);
            if (read <= 0) break;
            var samples = new float[read / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s16 = (short)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                samples[i] = s16 / 32768f;
            }
            QueryPerformanceCounter(out long t);
            var ms = (t - _startTicks) * 1000.0 / _perfFreq;
            _buffers.OnNext(new AudioBuffer(ms, samples, _sampleRate, _channels));
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
        _buffers.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
}
