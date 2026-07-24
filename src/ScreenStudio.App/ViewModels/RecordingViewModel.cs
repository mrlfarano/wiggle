using System.Windows.Input;
using Microsoft.UI.Xaml;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Recording;
using ScreenStudio.Native.Capture;

namespace ScreenStudio.App.ViewModels;

/// <summary>The captured data handed from RecordingViewModel to PostRecordViewModel on Stop.</summary>
public sealed record RecordingResult(List<byte[]> Frames, List<CursorEvent> Cursor, int Width, int Height);

/// <summary>Task #011 — RecordingViewModel. The bindable surface the RecordingOverlay XAML
/// binds to. Wraps <see cref="RecordingSession"/> (which exposes only polling State today,
/// ui-prd §9.2) and drives <see cref="IScreenCapture"/>. Exposes Start/Pause/Stop commands
/// + live status (REC indicator, elapsed timer) so the overlay chrome is purely declarative.
///
/// The VM polls RecordingSession on a DispatcherTimer to surface state changes (the engine
/// has no change event). All capture-thread calls are marshaled off the UI thread via the
/// capture observable's background subscription.</summary>
public sealed class RecordingViewModel : ViewModelBase, IDisposable
{
    private readonly RecordingSession _session;
    private readonly Func<CaptureOptions, IScreenCapture> _captureFactory;
    private IScreenCapture? _capture;
    private readonly DispatcherTimer _timer;

    // Accumulated capture data for the post-record window.
    private readonly List<CursorEvent> _cursorEvents = new();
    private readonly List<byte[]> _frames = new();
    private int _captureWidth = 1920;
    private int _captureHeight = 1080;

    private RecordingState _state = RecordingState.Idle;
    private TimeSpan _elapsed = TimeSpan.Zero;
    private DateTime _recordStartUtc;
    private CaptureTarget _target = CaptureTarget.FullScreen;
    private bool _captureMic = true;
    private bool _captureSystemAudio = true;
    private string _statusMessage = "Ready";

    /// <summary>Raised when recording stops, carrying the captured frames + cursor stream so
    /// the host can open the PostRecordWindow. Null frames when no capture occurred.</summary>
    public event Action<RecordingResult>? RecordingCompleted;

    public RecordingViewModel(RecordingSession session, Func<CaptureOptions, IScreenCapture> captureFactory)
    {
        _session = session;
        _captureFactory = captureFactory;
        StartCommand = new RelayCommand(_ => Start(), _ => State == RecordingState.Idle);
        PauseCommand = new RelayCommand(_ => Pause(), _ => State == RecordingState.Recording);
        ResumeCommand = new RelayCommand(_ => Resume(), _ => State == RecordingState.Paused);
        StopCommand = new RelayCommand(_ => Stop(), _ => State == RecordingState.Recording || State == RecordingState.Paused);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => PollState();
    }

    // ---- Bound properties ----

    /// <summary>Recording lifecycle state. Bound to the REC indicator visibility/style.</summary>
    public RecordingState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(CanShowRecIndicator));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Elapsed recording time (excludes paused intervals). Bound to the timer text.</summary>
    public TimeSpan Elapsed
    {
        get => _elapsed;
        private set
        {
            if (SetProperty(ref _elapsed, value))
                OnPropertyChanged(nameof(ElapsedText));
        }
    }

    public CaptureTarget Target { get => _target; set => SetProperty(ref _target, value); }
    public bool CaptureMic { get => _captureMic; set => SetProperty(ref _captureMic, value); }
    public bool CaptureSystemAudio { get => _captureSystemAudio; set => SetProperty(ref _captureSystemAudio, value); }

    private bool _captureWebcam;
    public bool CaptureWebcam { get => _captureWebcam; set => SetProperty(ref _captureWebcam, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    /// <summary>Public status setter for the host to report capture-unavailable states.</summary>
    public void SetStatus(string message) => StatusMessage = message;

    // ---- Derived booleans for XAML visibility bindings ----
    public bool IsRecording => State == RecordingState.Recording;
    public bool IsPaused => State == RecordingState.Paused;
    public bool CanShowRecIndicator => State == RecordingState.Recording || State == RecordingState.Paused;

    /// <summary>Elapsed as mm:ss for the timer display.</summary>
    public string ElapsedText => Elapsed.ToString(@"mm\:ss");

    /// <summary>Per-state button visibility (collapsed when not applicable).</summary>
    public bool ShowStart => State == RecordingState.Idle;
    public bool ShowPause => State == RecordingState.Recording;
    public bool ShowResume => State == RecordingState.Paused;
    public bool ShowStop => State == RecordingState.Recording || State == RecordingState.Paused;

    // ---- Commands ----
    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand StopCommand { get; }

    // ---- Actions ----

    public void Start()
    {
        if (State != RecordingState.Idle) return;
        try
        {
            _session.Start(0);
            _capture = _captureFactory(BuildOptions());
            _capture.Start(BuildOptions());

            // Accumulate captured frames + cursor events for the post-record window.
            _frames.Clear();
            _cursorEvents.Clear();
            _capture.Frames.Subscribe(new ActionObserver<CapturedFrame>(OnFrameCaptured));
            _capture.CursorEvents.Subscribe(new ActionObserver<CursorEvent>(OnCursorEvent));

            _recordStartUtc = DateTime.UtcNow;
            Elapsed = TimeSpan.Zero;
            State = RecordingState.Recording;
            StatusMessage = "Recording";
            _timer.Start();
        }
        catch (PlatformNotSupportedException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public void Pause()
    {
        if (State != RecordingState.Recording) return;
        _session.Pause(ElapsedMs());
        State = RecordingState.Paused;
        StatusMessage = "Paused";
    }

    public void Resume()
    {
        if (State != RecordingState.Paused) return;
        _session.Resume(ElapsedMs());
        State = RecordingState.Recording;
        StatusMessage = "Recording";
    }

    public void Stop()
    {
        if (State is not (RecordingState.Recording or RecordingState.Paused)) return;
        _session.Stop(ElapsedMs());
        _capture?.Stop();
        _timer.Stop();
        State = RecordingState.Stopped;
        StatusMessage = "Stopped";

        // Hand the captured data to the host so it can open the PostRecordWindow.
        var result = new RecordingResult(_frames.ToList(), _cursorEvents.ToList(), _captureWidth, _captureHeight);
        RecordingCompleted?.Invoke(result);
    }

    private void OnFrameCaptured(CapturedFrame frame)
    {
        _captureWidth = frame.Width;
        _captureHeight = frame.Height;
        // The FfmpegScreenCapture pins a BGRA byte[] and exposes its handle; copy the bytes
        // out so the buffer is owned by the VM. (WGC texture path would need a GPU readback.)
        var bytes = new byte[frame.Width * frame.Height * 4];
        System.Runtime.InteropServices.Marshal.Copy(frame.TextureHandle, bytes, 0, bytes.Length);
        _frames.Add(bytes);
    }

    private void OnCursorEvent(CursorEvent e) => _cursorEvents.Add(e);

    private CaptureOptions BuildOptions() => new()
    {
        Target = _target,
        TargetFrameRate = 30,
        CaptureCursor = true,
        CaptureMicrophone = _captureMic,
        CaptureSystemAudio = _captureSystemAudio,
    };

    private void PollState()
    {
        // Engine has no change event; refresh elapsed from session.
        Elapsed = TimeSpan.FromMilliseconds(_session.RecordedDurationMs > 0
            ? _session.RecordedDurationMs
            : (DateTime.UtcNow - _recordStartUtc).TotalMilliseconds);
    }

    private double ElapsedMs() => (DateTime.UtcNow - _recordStartUtc).TotalMilliseconds;

    public void Dispose()
    {
        _timer.Stop();
        _capture?.Dispose();
    }
}
