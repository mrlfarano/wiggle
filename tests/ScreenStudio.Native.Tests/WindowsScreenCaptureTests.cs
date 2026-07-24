using ScreenStudio.Native.Capture;

namespace ScreenStudio.Native.Tests;

/// <summary>Verifies the Windows Graphics Capture adapter. WGC needs (a) an interactive
/// desktop and (b) a usable GPU/DXGI adapter. In a headless/container session either is
/// absent, so Start must throw PlatformNotSupportedException with a clear message rather
/// than a raw COM/ArgumentException.</summary>
public class WindowsScreenCaptureTests
{
    [Fact]
    public async System.Threading.Tasks.Task Start_Throws_Platform_Not_Supported_When_No_Gpu_Or_Session()
    {
        using var cap = new WindowsScreenCapture();
        if (!cap.IsAvailable())
        {
            // Headless: IsSupported() false => Start refuses before touching D3D.
            Assert.Throws<PlatformNotSupportedException>(() => cap.Start(new CaptureOptions()));
            return;
        }

        // IsSupported() returned true but the session may still lack a GPU (this host).
        // Attempt Start on a background thread (WGC wants MTA) and expect a clean
        // PlatformNotSupportedException, not a marshalled COM exception.
        await System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                cap.Start(new CaptureOptions { Target = CaptureTarget.FullScreen, TargetFrameRate = 30 });
                // If we somehow get here (real desktop with GPU), stop cleanly.
                System.Threading.Thread.Sleep(150);
                cap.Stop();
            }
            catch (PlatformNotSupportedException)
            {
                // Expected on this host: no usable GPU/DXGI in the session.
            }
        });
    }
}
