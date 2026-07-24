using ScreenStudio.Core.Zoom;

namespace ScreenStudio.App.ViewModels;

/// <summary>Task #013 — ZoomSettingsViewModel. INPC wrapper around
/// <see cref="ZoomDetector"/> (its <see cref="ZoomMode"/> + <see cref="ZoomDetectionOptions"/>,
/// which are plain POCOs — ui-prd §9.1 gap). Drives the Zoom tab + aspect switcher of the
/// Style panel. The aspect-ratio change calls <see cref="Core.AspectRatio.AspectRatioConverter"/>
/// on the timeline's zoom program.</summary>
public sealed class ZoomSettingsViewModel : ViewModelBase
{
    private ZoomMode _mode = ZoomMode.Automatic;
    private readonly ZoomDetectionOptions _options = new();
    private Core.AspectRatio.AspectRatioMode _aspect = Core.AspectRatio.AspectRatioMode.Landscape;

    public ZoomMode Mode { get => _mode; set => SetProperty(ref _mode, value); }

    public double ZoomScale { get => _options.ZoomScale; set { _options.ZoomScale = value; OnPropertyChanged(); } }
    public double HoldMs { get => _options.HoldMs; set { _options.HoldMs = value; OnPropertyChanged(); } }
    public double MinKeyframeGapMs { get => _options.MinKeyframeGapMs; set { _options.MinKeyframeGapMs = value; OnPropertyChanged(); } }

    public Core.AspectRatio.AspectRatioMode AspectRatio
    {
        get => _aspect;
        set => SetProperty(ref _aspect, value);
    }

    /// <summary>All aspect-ratio presets for the switcher (Landscape/Portrait/social).</summary>
    public static Core.AspectRatio.AspectRatioMode[] AspectPresets => new[]
    {
        Core.AspectRatio.AspectRatioMode.Landscape,
        Core.AspectRatio.AspectRatioMode.Portrait,
        Core.AspectRatio.AspectRatioMode.TikTok,
        Core.AspectRatio.AspectRatioMode.InstagramReel,
        Core.AspectRatio.AspectRatioMode.YouTubeShort,
        Core.AspectRatio.AspectRatioMode.Square,
    };

    public ZoomDetectionOptions SnapshotOptions => _options;
}
