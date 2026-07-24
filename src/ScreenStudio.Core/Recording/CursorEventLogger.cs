using System.IO;
using ScreenStudio.Core.Cursor;

namespace ScreenStudio.Core.Recording;

/// <summary>Task 002 — Cursor event logger. Persists timestamped cursor events to disk as
/// they stream in from the capture hook, and can replay them for processing/export.
///
/// Format: a compact binary stream — magic, sample-rate-independent 64-bit double timestamps,
/// 32-bit X/Y, 8-bit button mask. Chosen so:
///  - writes are append-only (O(1) per event, safe to flush periodically)
///  - it is trivially unit-testable (write known events, read them back, compare)
/// The native WH_MOUSE_LL hook feeds this; here we only implement the persistence contract.</summary>
public sealed class CursorEventLogger : IDisposable
{
    private const uint Magic = 0x53534345; // "SSCE" (ScreenStudio Cursor Events)
    private const int RecordBytes = 8 + 4 + 4 + 1; // timestampMs + X + Y + buttons

    private readonly BinaryWriter _writer;
    private readonly FileStream _stream;
    private int _count;

    public CursorEventLogger(string path)
    {
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new BinaryWriter(_stream);
        _writer.Write(Magic);
    }

    /// <summary>Append a single cursor event. O(1); flush periodically via <see cref="Flush"/>.</summary>
    public void Log(CursorEvent e)
    {
        _writer.Write(e.TimestampMs);
        _writer.Write((float)e.Position.X);
        _writer.Write((float)e.Position.Y);
        _writer.Write((byte)e.Buttons);
        _count++;
    }

    /// <summary>Append many events (bulk log from a capture batch).</summary>
    public void LogRange(IEnumerable<CursorEvent> events)
    {
        foreach (var e in events) Log(e);
    }

    public void Flush() => _writer.Flush();

    public int Count => _count;

    public void Dispose()
    {
        Flush();
        _writer.Dispose();
        _stream.Dispose();
    }

    /// <summary>Read a cursor-event log file back into an ordered list. Static so tests/replay
    /// don't need a live writer.</summary>
    public static List<CursorEvent> Read(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);
        var magic = br.ReadUInt32();
        if (magic != Magic)
            throw new InvalidDataException($"Bad magic 0x{magic:X8}; expected 0x{Magic:X8}");

        var result = new List<CursorEvent>();
        while (fs.Position + RecordBytes <= fs.Length)
        {
            var t = br.ReadDouble();
            var x = br.ReadSingle();
            var y = br.ReadSingle();
            var b = (CursorButtonState)br.ReadByte();
            result.Add(new CursorEvent(t, new Math.Vec2(x, y), b));
        }
        return result;
    }
}
