using ScreenStudio.App.Services;
using ScreenStudio.App.ViewModels;
using ScreenStudio.Core.Smoothing;
using CoreZoomMode = ScreenStudio.Core.Zoom.ZoomMode;

namespace ScreenStudio.App.ViewModels;

/// <summary>Task #015 — SettingsViewModel. The bindable surface for the SettingsWindow. Wraps
/// <see cref="SettingsService.AppSettings"/> with INPC + exposes device lists + save command.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _service;

    public List<string> Microphones { get; }
    public List<string> ScreenTargets { get; }

    private SmoothingIntensity _defaultSmoothing;
    public SmoothingIntensity DefaultSmoothing { get => _defaultSmoothing; set => SetProperty(ref _defaultSmoothing, value); }

    private CoreZoomMode _defaultZoomMode;
    public CoreZoomMode DefaultZoomMode { get => _defaultZoomMode; set => SetProperty(ref _defaultZoomMode, value); }

    private string? _preferredMic;
    public string? PreferredMic { get => _preferredMic; set => SetProperty(ref _preferredMic, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public static string SettingsFilePath => SettingsService.SettingsFilePath;

    public SettingsViewModel(SettingsService service)
    {
        _service = service;
        var s = service.Current;
        _defaultSmoothing = s.DefaultSmoothing;
        _defaultZoomMode = s.DefaultZoomMode;
        _preferredMic = s.PreferredMicrophone;
        Microphones = DeviceEnumeration.ListMicrophones();
        ScreenTargets = DeviceEnumeration.ListScreenTargets();
    }

    /// <summary>Persist current settings to disk.</summary>
    public void Save()
    {
        _service.Current.DefaultSmoothing = _defaultSmoothing;
        _service.Current.DefaultZoomMode = _defaultZoomMode;
        _service.Current.PreferredMicrophone = _preferredMic;
        _service.Save();
        StatusMessage = $"Saved to {SettingsFilePath}";
    }
}
