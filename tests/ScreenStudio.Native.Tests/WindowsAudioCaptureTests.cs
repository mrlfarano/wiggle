using ScreenStudio.Native.Audio;

namespace ScreenStudio.Native.Tests;

/// <summary>Verifies the audio capture adapter. AudioGraph requires real audio endpoints
/// (absent in a headless session); when none exist, IsAvailable() is false and Start must
/// throw PlatformNotSupportedException rather than crash.</summary>
public class WindowsAudioCaptureTests
{
    [Fact]
    public void Reports_Availability_And_Refuses_Start_When_No_Endpoints()
    {
        using var cap = new WindowsAudioCapture();
        if (!cap.IsAvailable())
        {
            Assert.Throws<PlatformNotSupportedException>(() => cap.Start(microphone: true, systemLoopback: true));
            return;
        }

        // Endpoints present: Start/Stop must not throw against the live AudioGraph.
        cap.Start(microphone: true, systemLoopback: false);
        System.Threading.Thread.Sleep(200);
        cap.Stop();
    }
}
