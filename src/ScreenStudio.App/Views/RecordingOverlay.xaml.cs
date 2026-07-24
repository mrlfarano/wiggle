using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ScreenStudio.App.ViewModels;
using ScreenStudio.Native.Capture;

namespace ScreenStudio.App.Views;

/// <summary>Task #011 — the floating recording overlay (ui-prd §6.1). Minimal, always-on-top
/// chrome: REC indicator, live timer, target selector, mic/audio toggles, transport controls.
/// Binds to <see cref="RecordingViewModel"/>; the overlay stays out of the way during recording
/// (Loom's "recorder stays out of the way" model, research §2.7).</summary>
public sealed partial class RecordingOverlay : UserControl
{
    public RecordingViewModel ViewModel { get; private set; }

    /// <summary>Used by the XAML loader. The ViewModel is wired later via
    /// <see cref="InitializeOverlay"/> (the host constructs this control in code-behind with the
    /// VM, so x:Bind has a non-null root at load time).</summary>
    public RecordingOverlay()
    {
        InitializeComponent();
    }

    public RecordingOverlay(RecordingViewModel viewModel) : this()
    {
        ViewModel = viewModel;
    }

    /// <summary>Wire the ViewModel after XAML instantiation and refresh the compiled bindings.</summary>
    public void InitializeOverlay(RecordingViewModel viewModel)
    {
        ViewModel = viewModel;
        Bindings.Update();
    }

    /// <summary>Rec-indicator color: red when recording, amber when paused, dim otherwise.
    /// Null-safe: returns the dim grey indicator before the VM is wired.</summary>
    public SolidColorBrush RecIndicatorBrush
    {
        get
        {
            var vm = ViewModel;
            if (vm is null)
                return new SolidColorBrush(Windows.UI.Color.FromArgb(120, 128, 128, 128));
            return new SolidColorBrush(vm.IsRecording
                ? Windows.UI.Color.FromArgb(255, 232, 65, 80)
                : vm.IsPaused
                    ? Windows.UI.Color.FromArgb(255, 210, 153, 34)
                    : Windows.UI.Color.FromArgb(120, 128, 128, 128));
        }
    }

    /// <summary>Activate recording from the tray menu (Start command).</summary>
    public void ActivateRecording()
    {
        if (ViewModel is not null && ViewModel.State == ScreenStudio.Core.Recording.RecordingState.Idle)
            ViewModel.StartCommand.Execute(null);
    }

    private void OnTargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        var idx = TargetSelector.SelectedIndex;
        ViewModel.Target = idx switch
        {
            0 => CaptureTarget.FullScreen,
            1 => CaptureTarget.Display,
            2 => CaptureTarget.Window,
            3 => CaptureTarget.Region,
            _ => CaptureTarget.FullScreen,
        };
        // When "Window" is selected, populate the ComboBox with real on-screen windows.
        if (idx == 2) PopulateWindowTargets();
    }

    /// <summary>Populate the target selector with actual open windows (via WindowEnumeration).</summary>
    private void PopulateWindowTargets()
    {
        try
        {
            var windows = Services.WindowEnumeration.ListWindows();
            if (windows.Count == 0) return;
            // Replace the static items with real window titles (keeping the first 4 generic options
            // + appending discovered windows). The selected window's HWND goes into CaptureOptions.TargetHandle.
            TargetSelector.Items.Clear();
            TargetSelector.Items.Add(new ComboBoxItem { Content = "Full Screen" });
            TargetSelector.Items.Add(new ComboBoxItem { Content = "Display" });
            TargetSelector.Items.Add(new ComboBoxItem { Content = "Window…" });
            foreach (var w in windows)
                TargetSelector.Items.Add(new ComboBoxItem { Content = w.Title, Tag = w.Handle });
            TargetSelector.SelectedIndex = 0;
        }
        catch { /* best-effort */ }
    }

    private void OnMicToggled(object sender, RoutedEventArgs e)
    { if (ViewModel is not null) ViewModel.CaptureMic = MicToggle.IsOn; }
    private void OnAudioToggled(object sender, RoutedEventArgs e)
    { if (ViewModel is not null) ViewModel.CaptureSystemAudio = AudioToggle.IsOn; }
    private void OnWebcamToggled(object sender, RoutedEventArgs e)
    { if (ViewModel is not null) ViewModel.CaptureWebcam = WebcamToggle.IsOn; }
}
