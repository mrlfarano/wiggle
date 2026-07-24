using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScreenStudio.App.ViewModels;
using ScreenStudio.Core.Smoothing;
using CoreZoomMode = ScreenStudio.Core.Zoom.ZoomMode;

namespace ScreenStudio.App.Views;

/// <summary>Task #015 — SettingsWindow. App-level preferences that persist across sessions
/// (ui-prd §6.5 + §9.1): default smoothing/zoom, preferred mic, hotkeys. Standard SettingsPage
/// with Fluent controls.</summary>
public sealed partial class SettingsWindow : Microsoft.UI.Xaml.Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Title = "Screen Studio — Settings";

        // Populate device lists.
        foreach (var mic in _vm.Microphones) MicBox.Items.Add(new ComboBoxItem { Content = mic });
        if (!string.IsNullOrEmpty(_vm.PreferredMic))
        {
            var idx = _vm.Microphones.IndexOf(_vm.PreferredMic!);
            MicBox.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else if (MicBox.Items.Count > 0) MicBox.SelectedIndex = 0;

        // Reflect current defaults.
        SmoothingBox.SelectedIndex = (int)_vm.DefaultSmoothing;
        ZoomModeSwitch.IsOn = _vm.DefaultZoomMode == CoreZoomMode.Automatic;

        SettingsPathText.Text = $"Settings file: {_vm.SettingsFilePathString()}";
    }

    private void OnSmoothingChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.DefaultSmoothing = SmoothingBox.SelectedIndex switch
        {
            0 => SmoothingIntensity.None,
            1 => SmoothingIntensity.Light,
            2 => SmoothingIntensity.Medium,
            3 => SmoothingIntensity.Heavy,
            _ => SmoothingIntensity.Medium,
        };
    }

    private void OnZoomModeToggled(object sender, RoutedEventArgs e)
        => _vm.DefaultZoomMode = ZoomModeSwitch.IsOn ? CoreZoomMode.Automatic : CoreZoomMode.Manual;

    private void OnMicChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MicBox.SelectedItem is ComboBoxItem item)
            _vm.PreferredMic = item.Content as string;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        _vm.Save();
        StatusText.Text = _vm.StatusMessage;
    }
}

internal static class SettingsVmExtensions
{
    public static string SettingsFilePathString(this SettingsViewModel vm) => SettingsViewModel.SettingsFilePath;
}
