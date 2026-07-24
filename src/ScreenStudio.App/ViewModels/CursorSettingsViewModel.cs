using ScreenStudio.Core.Cursor;

namespace ScreenStudio.App.ViewModels;

/// <summary>Task #013 — CursorSettingsViewModel. INPC wrapper around the plain
/// <see cref="CursorCustomization"/> POCO (ui-prd §9.1 gap: engine has no notifications).
/// Drives the Cursor tab of the Style panel.</summary>
public sealed class CursorSettingsViewModel : ViewModelBase
{
    private readonly CursorCustomization _settings;
    public CursorCustomization Settings => _settings;

    public CursorSettingsViewModel(CursorCustomization? settings = null)
    {
        _settings = settings ?? new CursorCustomization();
    }

    public double SizeMultiplier
    {
        get => _settings.SizeMultiplier;
        set { _settings.SizeMultiplier = value; OnPropertyChanged(); }
    }

    public bool AutoHideEnabled
    {
        get => _settings.AutoHideEnabled;
        set { _settings.AutoHideEnabled = value; OnPropertyChanged(); }
    }

    public double AutoHideTimeoutMs
    {
        get => _settings.AutoHideTimeoutMs;
        set { _settings.AutoHideTimeoutMs = value; OnPropertyChanged(); }
    }

    public bool LoopEnabled
    {
        get => _settings.LoopEnabled;
        set { _settings.LoopEnabled = value; OnPropertyChanged(); }
    }

    public string? CustomImagePath
    {
        get => _settings.CustomImagePath;
        set { _settings.CustomImagePath = value; OnPropertyChanged(); }
    }

    /// <summary>Size presets for the slider detents.</summary>
    public static double[] SizePresets => CursorCustomization.SizePresets;
}
