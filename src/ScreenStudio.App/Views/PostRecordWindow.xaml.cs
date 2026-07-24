using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ScreenStudio.App.ViewModels;
using ScreenStudio.Core.Export;
using ScreenStudio.Core.Smoothing;

namespace ScreenStudio.App.Views;

/// <summary>Task #012 — the value-reveal window. Shows the auto-applied-zoom preview (left)
/// and the export panel (right). On launch it renders a preview frame at ~25% to show off the
/// auto-zoom immediately. Export drives PostRecordViewModel.ExportAsync → real MP4 on disk.</summary>
public sealed partial class PostRecordWindow : Microsoft.UI.Xaml.Window
{
    private readonly PostRecordViewModel _vm;
    private CancellationTokenSource? _exportCts;

    public PostRecordWindow(PostRecordViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Title = "Screen Studio — Recording";

        // Construct the Style panel with its VMs and wire changes → preview re-render.
        var cursorVm = new CursorSettingsViewModel(vm.CursorStyle);
        var zoomVm = new ZoomSettingsViewModel();
        StyleControls.InitializePanel(cursorVm, zoomVm,
            setSmoothing: intensity => { vm.Smoother = new CursorSmoother(intensity); },
            setAspect: _ => { /* aspect conversion applied at export; preview re-renders */ });

        // Wire the new feature callbacks (motion blur, background, keystrokes).
        StyleControls.SetEffectCallbacks(
            setMotionBlur: intensity => { vm.MotionBlurIntensity = intensity; },
            setBackground: (enabled, padding, shadow, border) =>
            {
                vm.ApplyBackground = enabled;
                vm.VisualFrame = new FrameCompositor.VisualFrame(
                    0x2e, 0x1a, 0x1a, padding, shadow, 100, 0, border, 0, 255, 0);
            },
            setKeystrokes: show => { vm.ShowKeystrokes = show; });

        StyleControls.StyleChanged += () => { _ = RenderInitialPreview(); };

        // Wire the timeline panel (power-user; collapsed until Edit zoom toggled).
        var timelineVm = new TimelineViewModel(vm.Timeline);
        Timeline.InitializeTimeline(timelineVm);
        EditZoomToggle.Toggled += (_, _) =>
        {
            Timeline.Visibility = EditZoomToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        };

        PopulatePresetChips();
        _ = RenderInitialPreview();
    }

    private async Task RenderInitialPreview()
    {
        // Render preview immediately (the "magic moment" — ui-prd §6.2).
        await _vm.RenderPreviewAsync(0.25);
        UpdatePreviewImage();
    }

    private async void UpdatePreviewImage()
    {
        if (_vm.PreviewBytes is not null)
        {
            PreviewImage.Source = await PostRecordViewModel.ToWriteableBitmapAsync(
                _vm.PreviewBytes, _vm.PreviewWidth, _vm.PreviewHeight);
        }
    }

    private void PopulatePresetChips()
    {
        foreach (var preset in ExportSettingsViewModel.Presets)
        {
            var chip = new Button
            {
                Content = preset.Name,
                Padding = new Thickness(12, 4, 12, 4),
                CornerRadius = new CornerRadius(14),
            };
            chip.Click += (_, _) =>
            {
                _vm.ExportSettings.ApplyPreset(preset);
                Validate();
            };
            PresetChips.Items.Add(chip);
        }
    }

    private void OnCodecChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.ExportSettings.Codec = CodecBox.SelectedIndex switch
        {
            0 => ScreenStudio.Core.Export.VideoCodec.H264,
            1 => ScreenStudio.Core.Export.VideoCodec.HEVC,
            2 => ScreenStudio.Core.Export.VideoCodec.GIF,
            3 => ScreenStudio.Core.Export.VideoCodec.WEBM,
            _ => ScreenStudio.Core.Export.VideoCodec.H264,
        };
        // GIF/WebM use different extensions.
        var ext = CodecBox.SelectedIndex switch
        {
            2 => ".gif",
            3 => ".webm",
            _ => ".mp4",
        };
        var currentPath = OutputPathBox.Text;
        if (!string.IsNullOrEmpty(currentPath))
        {
            var dir = System.IO.Path.GetDirectoryName(currentPath) ?? "";
            var name = System.IO.Path.GetFileNameWithoutExtension(currentPath);
            OutputPathBox.Text = System.IO.Path.Combine(dir, name + ext);
        }
        Validate();
    }

    private void Validate()
    {
        _vm.ExportSettings.OutputPath = OutputPathBox.Text;
        ValidationText.Text = _vm.ExportSettings.ValidationError;
        ExportButton.IsEnabled = _vm.ExportSettings.CanExport;
    }

    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        Validate();
        if (!_vm.ExportSettings.CanExport) return;

        ExportProgress.Visibility = Visibility.Visible;
        ExportButton.IsEnabled = false;
        _exportCts = new CancellationTokenSource();

        // Drive export on a background task; poll progress on the UI thread.
        var exportTask = _vm.ExportAsync(_exportCts.Token);
        var progressLoop = Task.Run(async () =>
        {
            while (!exportTask.IsCompleted)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ExportProgress.Value = _vm.ExportProgressValue;
                    StatusText.Text = _vm.StatusMessage;
                });
                await Task.Delay(50);
            }
        });

        await exportTask;
        ExportProgress.Value = _vm.ExportProgressValue;
        StatusText.Text = _vm.StatusMessage;
        ExportProgress.Visibility = Visibility.Collapsed;
        ExportButton.IsEnabled = true;
    }
}
