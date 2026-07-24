using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScreenStudio.App.ViewModels;
using ScreenStudio.Core.Smoothing;
using CoreZoomMode = ScreenStudio.Core.Zoom.ZoomMode;

namespace ScreenStudio.App.Views;

/// <summary>Task #013 — the Style panel. Tabbed controls for Cursor / Smoothing / Zoom + the
/// prominent aspect-ratio switcher. Binds to <see cref="CursorSettingsViewModel"/> +
/// <see cref="ZoomSettingsViewModel"/>; changes trigger a preview re-render via the
/// StyleChanged event (the host re-renders the preview frame).</summary>
public sealed partial class StylePanel : UserControl
{
    public CursorSettingsViewModel CursorVm { get; private set; } = null!;
    public ZoomSettingsViewModel ZoomVm { get; private set; } = null!;

    /// <summary>Raised when any style setting changes (host re-renders the preview).</summary>
    public event Action? StyleChanged;

    private Action<SmoothingIntensity>? _setSmoothing;
    private Action<Core.AspectRatio.AspectRatioMode>? _setAspect;

    public StylePanel() { InitializeComponent(); }

    /// <summary>Initialize after XAML instantiation (host passes in VMs + callbacks).</summary>
    public void InitializePanel(
        CursorSettingsViewModel cursorVm,
        ZoomSettingsViewModel zoomVm,
        Action<SmoothingIntensity> setSmoothing,
        Action<Core.AspectRatio.AspectRatioMode> setAspect)
    {
        CursorVm = cursorVm;
        ZoomVm = zoomVm;
        _setSmoothing = setSmoothing;
        _setAspect = setAspect;
        PopulateAspectBox();
        SmoothingBox.SelectedIndex = 2; // Medium default
        Bindings.Update();
    }

    private void PopulateAspectBox()
    {
        foreach (var mode in ZoomSettingsViewModel.AspectPresets)
            AspectBox.Items.Add(new ComboBoxItem { Content = mode.Label, Tag = mode });
        AspectBox.SelectedIndex = 0;
    }

    private void OnAspectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AspectBox.SelectedItem is ComboBoxItem item && item.Tag is Core.AspectRatio.AspectRatioMode mode)
        {
            ZoomVm.AspectRatio = mode;
            _setAspect?.Invoke(mode);
            StyleChanged?.Invoke();
        }
    }

    private void OnSmoothingChanged(object sender, SelectionChangedEventArgs e)
    {
        var intensity = SmoothingBox.SelectedIndex switch
        {
            0 => SmoothingIntensity.None,
            1 => SmoothingIntensity.Light,
            2 => SmoothingIntensity.Medium,
            3 => SmoothingIntensity.Heavy,
            _ => SmoothingIntensity.Medium,
        };
        _setSmoothing?.Invoke(intensity);
        StyleChanged?.Invoke();
    }

    private void OnZoomModeToggled(object sender, RoutedEventArgs e)
    {
        ZoomVm.Mode = ZoomModeSwitch.IsOn ? CoreZoomMode.Automatic : CoreZoomMode.Manual;
        StyleChanged?.Invoke();
    }

    // ---- Motion blur ----
    private Action<double>? _setMotionBlur;
    private Action<bool, int, int, int>? _setBackground;

    /// <summary>Wire motion-blur + background + keycap callbacks (from PostRecordWindow).</summary>
    public void SetEffectCallbacks(
        Action<double>? setMotionBlur,
        Action<bool, int, int, int>? setBackground,
        Action<bool>? setKeystrokes)
    {
        _setMotionBlur = setMotionBlur;
        _setBackground = setBackground;
        _setKeystrokes = setKeystrokes;
    }
    private Action<bool>? _setKeystrokes;

    private void OnMotionBlurChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _setMotionBlur?.Invoke(e.NewValue);
        StyleChanged?.Invoke();
    }

    private void OnBackgroundToggled(object sender, RoutedEventArgs e)
    {
        _setBackground?.Invoke(BackgroundToggle.IsOn, (int)PaddingSlider.Value, (int)ShadowSlider.Value, (int)BorderSlider.Value);
        StyleChanged?.Invoke();
    }

    private void OnPaddingChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (BackgroundToggle.IsOn)
            _setBackground?.Invoke(true, (int)e.NewValue, (int)ShadowSlider.Value, (int)BorderSlider.Value);
    }

    private void OnShadowChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (BackgroundToggle.IsOn)
            _setBackground?.Invoke(true, (int)PaddingSlider.Value, (int)e.NewValue, (int)BorderSlider.Value);
    }

    private void OnBorderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (BackgroundToggle.IsOn)
            _setBackground?.Invoke(true, (int)PaddingSlider.Value, (int)ShadowSlider.Value, (int)e.NewValue);
    }

    private void OnKeystrokeToggled(object sender, RoutedEventArgs e)
    {
        _setKeystrokes?.Invoke(KeystrokeToggle.IsOn);
        StyleChanged?.Invoke();
    }
}
