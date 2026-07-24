using Microsoft.UI.Xaml;
using ScreenStudio.App.ViewModels;

namespace ScreenStudio.App;

/// <summary>App root: bootstraps the WinUI application host and main window.
/// Composition root for the engine services (RecordingSession, IScreenCapture,
/// ExportPipeline) — wired via <see cref="CompositionRoot"/> per ui-prd.md §7.</summary>
public partial class App : Application
{
    private MainWindow? _window;
    private RecordingViewModel? _recordingVm;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _recordingVm = CompositionRoot.CreateRecordingViewModel();
        _window = new MainWindow(_recordingVm);
        _window.Activate();
    }
}
