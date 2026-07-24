using ScreenStudio.Native.Audio;

namespace ScreenStudio.App.Services;

/// <summary>Task #015 — device enumeration surface (ui-prd §9.3 gap). The capture adapters
/// need a target/mic picker; this surfaces the available devices. Microphone enumeration uses
/// <see cref="FfmpegAudioCapture.ListMicrophones"/>; screen targets (full/display/window) are
/// surfaced as static options (the WGC window-enumeration path needs a Win32 EnumWindows
/// bridge — added here as a TODO for full window-picking; fullscreen/display work today).</summary>
public static class DeviceEnumeration
{
    /// <summary>List available microphone device names. Empty if none / ffmpeg absent.</summary>
    public static List<string> ListMicrophones()
    {
        try { return FfmpegAudioCapture.ListMicrophones(); }
        catch { return new List<string>(); }
    }

    /// <summary>Screen-capture target labels (FullScreen is always available; Display/Window
    /// depend on the running session).</summary>
    public static List<string> ListScreenTargets() => new() { "Full Screen", "Display", "Window", "Region" };
}
