using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Recording;

namespace ScreenStudio.Core.Tests.Recording;

public class CursorEventLoggerTests
{
    private static CursorEvent E(double t, double x, double y, CursorButtonState b = CursorButtonState.None)
        => new(t, new Vec2(x, y), b);

    [Fact]
    public void Write_Then_Read_Roundtrips_Events()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ssc_log_{Guid.NewGuid():N}.bin");
        var written = new List<CursorEvent>
        {
            E(0,     10, 10),
            E(16.6,  40, 12, CursorButtonState.Left),
            E(33.3,  70, 15),
        };
        try
        {
            using (var logger = new CursorEventLogger(path))
                logger.LogRange(written);

            var read = CursorEventLogger.Read(path);
            Assert.Equal(written.Count, read.Count);
            for (int i = 0; i < written.Count; i++)
            {
                Assert.Equal(written[i].TimestampMs, read[i].TimestampMs, 3);
                Assert.Equal(written[i].Position.X, read[i].Position.X, 3);
                Assert.Equal(written[i].Position.Y, read[i].Position.Y, 3);
                Assert.Equal(written[i].Buttons, read[i].Buttons);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Read_Rejects_Bad_Magic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ssc_bad_{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            Assert.Throws<InvalidDataException>(() => CursorEventLogger.Read(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Count_Tracks_Logged_Events()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ssc_cnt_{Guid.NewGuid():N}.bin");
        try
        {
            using var logger = new CursorEventLogger(path);
            logger.Log(E(0, 1, 1));
            logger.Log(E(1, 2, 2));
            logger.Log(E(2, 3, 3));
            Assert.Equal(3, logger.Count);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public class RecordingSessionTests
{
    private static CursorEvent E(double t, double x = 0, double y = 0)
        => new(t, new Vec2(x, y), CursorButtonState.None);

    [Fact]
    public void Lifecycle_Start_Pause_Resume_Stop()
    {
        var s = new RecordingSession();
        Assert.Equal(RecordingState.Idle, s.State);

        s.Start(0);
        Assert.Equal(RecordingState.Recording, s.State);
        s.Ingest(E(10, 1, 1));
        s.Ingest(E(20, 2, 2));

        s.Pause(30);
        Assert.Equal(RecordingState.Paused, s.State);
        s.Ingest(E(40, 3, 3)); // dropped — paused
        Assert.Equal(2, s.CursorEvents.Count);

        s.Resume(100);
        Assert.Equal(RecordingState.Recording, s.State);
        s.Ingest(E(110, 4, 4));
        s.Stop(200);
        Assert.Equal(RecordingState.Stopped, s.State);

        Assert.Equal(3, s.CursorEvents.Count); // only recorded-while-active events
    }

    [Fact]
    public void RecordedDuration_Excludes_Paused_Intervals()
    {
        var s = new RecordingSession();
        s.Start(0);
        s.Pause(100);
        s.Resume(200);   // 100ms paused
        s.Stop(400);
        // total span 400ms - 100ms paused = 300ms recorded
        Assert.Equal(300, s.RecordedDurationMs, 1);
    }

    [Fact]
    public void Invalid_Transitions_Throw()
    {
        var s = new RecordingSession();
        Assert.Throws<InvalidOperationException>(() => s.Pause(0));   // from Idle
        Assert.Throws<InvalidOperationException>(() => s.Resume(0));   // from Idle
        Assert.Throws<InvalidOperationException>(() => s.Stop(0));     // from Idle (never started)

        s.Start(0);
        Assert.Throws<InvalidOperationException>(() => s.Resume(0));   // already recording
    }

    [Fact]
    public void Stop_Is_Idempotent()
    {
        var s = new RecordingSession();
        s.Start(0);
        s.Stop(100);
        s.Stop(200); // no throw
        Assert.Equal(RecordingState.Stopped, s.State);
    }

    [Fact]
    public void IsRecordedTime_Respects_Pause()
    {
        var s = new RecordingSession();
        s.Start(0);
        s.Pause(50);
        s.Resume(100);
        s.Stop(200);
        Assert.True(s.IsRecordedTime(25));   // before pause
        Assert.False(s.IsRecordedTime(75));  // during pause
        Assert.True(s.IsRecordedTime(150));  // after resume
        Assert.False(s.IsRecordedTime(-10)); // before start
    }

    [Fact]
    public void Start_Clears_Previous_Events()
    {
        var s = new RecordingSession();
        s.Start(0);
        s.Ingest(E(10, 1, 1));
        s.Stop(20);
        // Re-start a new session after reset
        s.Reset();
        s.Start(100);
        Assert.Empty(s.CursorEvents);
    }
}
