using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScreenStudio.App.Services;
using ScreenStudio.App.ViewModels;
using ScreenStudio.App.Views;

namespace ScreenStudio.App;

/// <summary>Main window — hosts the RecordingOverlay (011) at the bottom; on Stop, opens the
/// PostRecordWindow (012) with the captured frames + cursor stream. The overlay is constructed
/// in code-behind (not inline XAML) so its compiled x:Bind expressions have a non-null
/// ViewModel root at load time — instantiating it inline with a null VM crashes the XAML
/// runtime with a stowed exception (0xC000027B) in this unpackaged WinUI 3 host.</summary>
public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    public RecordingViewModel RecordingVm { get; }
    private readonly RecordingOverlay _overlay;
    private readonly TrayController? _tray;

    public MainWindow(RecordingViewModel recordingVm)
    {
        InitializeComponent();
        RecordingVm = recordingVm;
        Title = "Wiggle";

        // Construct the overlay in code with its VM (so x:Bind has a live root), then host it.
        _overlay = new RecordingOverlay(recordingVm);
        OverlayHost.Children.Add(_overlay);

        // On Stop, open the PostRecordWindow with the captured data.
        RecordingVm.RecordingCompleted += OnRecordingCompleted;

        // System tray icon with context menu (best-effort; harmless if it fails).
        try
        {
            _tray = new TrayController(this,
                onNewRecording: () => DispatcherQueue.TryEnqueue(() => { _overlay.ActivateRecording(); }),
                onSettings: () => DispatcherQueue.TryEnqueue(() => OnSettingsClicked(this, new RoutedEventArgs())),
                onQuit: () => Microsoft.UI.Xaml.Application.Current.Exit());
        }
        catch { _tray = null; }
    }

    private void OnRecordingCompleted(RecordingResult result)
    {
        // Marshal to the UI thread (the capture subscription may invoke this off-thread).
        DispatcherQueue.TryEnqueue(() =>
        {
            if (result.Frames.Count == 0)
            {
                // No frames captured (e.g. capture unavailable) — show a light message instead.
                RecordingVm.SetStatus("No frames captured (capture unavailable).");
                return;
            }
            var postVm = new PostRecordViewModel(
                result.Frames, result.Cursor, result.Width, result.Height, DispatcherQueue);
            var window = new PostRecordWindow(postVm);
            window.Activate();
        });
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var service = new SettingsService();
        var vm = new SettingsViewModel(service);
        var settingsWindow = new SettingsWindow(vm);
        settingsWindow.Activate();
    }
}
