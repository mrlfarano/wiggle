using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Recording;

/// <summary>Recording lifecycle states (PRD P0: "Basic recording controls start/stop/pause").</summary>
public enum RecordingState { Idle, Recording, Paused, Stopped }

/// <summary>Task 002 — RecordingSession state machine + buffer coordination. Tracks the
/// recording lifecycle, accumulates cursor events (for later processing/export), and records
/// timing so downstream stages know the true recorded span (excluding paused intervals).
///
/// This is the headless-testable core of the recording controls; the native capture adapter
/// (WindowsScreenCapture) feeds frame/cursor data into it. State transitions are validated:
/// Pause from Idle throws, Stop is idempotent, etc.</summary>
public sealed class RecordingSession
{
    private readonly List<CursorEvent> _events = new();
    private readonly List<(double start, double end)> _pausedIntervals = new();
    private RecordingState _state = RecordingState.Idle;
    private double _startMs;
    private double _pauseStartMs;
    private double _stopMs;

    public RecordingState State => _state;
    public IReadOnlyList<CursorEvent> CursorEvents => _events;
    public double StartMs => _startMs;

    /// <summary>Begin recording. Must be called from Idle (use <see cref="Reset"/> to reuse a
    /// stopped session).</summary>
    public void Start(double nowMs)
    {
        if (_state != RecordingState.Idle)
            throw new InvalidOperationException($"Cannot Start from state {_state}; call Reset() first");
        _state = RecordingState.Recording;
        _startMs = nowMs;
        _stopMs = 0;
        _events.Clear();
        _pausedIntervals.Clear();
    }

    /// <summary>Return to Idle so the session can be started again. Clears accumulated data.</summary>
    public void Reset()
    {
        _state = RecordingState.Idle;
        _events.Clear();
        _pausedIntervals.Clear();
        _startMs = _stopMs = _pauseStartMs = 0;
    }

    /// <summary>Pause capture. Events arriving while Paused are dropped.</summary>
    public void Pause(double nowMs)
    {
        if (_state != RecordingState.Recording)
            throw new InvalidOperationException($"Cannot Pause from state {_state}");
        _state = RecordingState.Paused;
        _pauseStartMs = nowMs;
    }

    /// <summary>Resume after a Pause.</summary>
    public void Resume(double nowMs)
    {
        if (_state != RecordingState.Paused)
            throw new InvalidOperationException($"Cannot Resume from state {_state}");
        _pausedIntervals.Add((_pauseStartMs, nowMs));
        _state = RecordingState.Recording;
    }

    /// <summary>Stop recording. Idempotent — safe to call multiple times.</summary>
    public void Stop(double nowMs)
    {
        if (_state == RecordingState.Stopped) return;
        if (_state == RecordingState.Paused)
            _pausedIntervals.Add((_pauseStartMs, nowMs));
        if (_state == RecordingState.Idle)
            throw new InvalidOperationException("Cannot Stop from Idle (never started)");
        _state = RecordingState.Stopped;
        _stopMs = nowMs;
    }

    /// <summary>Ingest a cursor event from the capture hook. Only retained when Recording.</summary>
    public void Ingest(CursorEvent e)
    {
        if (_state == RecordingState.Recording)
            _events.Add(e);
    }

    /// <summary>Total recorded duration in ms, excluding paused intervals.</summary>
    public double RecordedDurationMs
    {
        get
        {
            if (_state == RecordingState.Idle) return 0;
            var end = _state == RecordingState.Stopped ? _stopMs : _startMs;
            var total = end - _startMs;
            foreach (var (s, e) in _pausedIntervals) total -= (e - s);
            return System.Math.Max(0, total);
        }
    }

    /// <summary>True if time t falls within a recorded (non-paused) interval.</summary>
    public bool IsRecordedTime(double tMs)
    {
        if (tMs < _startMs) return false;
        foreach (var (s, e) in _pausedIntervals)
            if (tMs >= s && tMs <= e) return false;
        return true;
    }
}
