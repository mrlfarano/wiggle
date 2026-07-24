using ScreenStudio.App.ViewModels;
using ScreenStudio.Core.Recording;
using ScreenStudio.Native.Capture;

namespace ScreenStudio.App;

/// <summary>Task #010/011 — composition root. Wires the engine services (RecordingSession +
/// IScreenCapture) into the ViewModels the XAML binds to. This is the app shell host the
/// codebase was missing (ui-prd §5, §9). Resolves the capture implementation at runtime:
/// prefers FfmpegScreenCapture (works without a GPU) and falls back to WindowsScreenCapture.</summary>
public static class CompositionRoot
{
    public static RecordingViewModel CreateRecordingViewModel()
    {
        var session = new RecordingSession();
        return new RecordingViewModel(session, opts => CreateScreenCapture());
    }

    /// <summary>Pick the best available screen capture implementation for this environment.</summary>
    public static IScreenCapture CreateScreenCapture()
    {
        // FfmpegScreenCapture works without a GPU (gdigrab); prefer it for broad compatibility.
        if (FfmpegScreenCapture.IsAvailableStatic())
            return new FfmpegScreenCapture();
        // WindowsScreenCapture (WGC) needs a GPU; IsAvailable gates it.
        var wgc = new WindowsScreenCapture();
        if (wgc.IsAvailable()) return wgc;
        throw new PlatformNotSupportedException(
            "No screen capture backend available (need ffmpeg on PATH or a GPU + interactive desktop).");
    }
}
