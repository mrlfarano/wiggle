using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

using ScreenStudio.App.ViewModels;

namespace ScreenStudio.App.Views;

/// <summary>Task #014 — the power-user timeline editor (ui-prd §6.3). A Canvas-based track
/// showing zoom keyframes (draggable dots) + segments + a scrubbing playhead. Collapsed by
/// default (the host toggles visibility via "Edit zoom"). Built on <see cref="TimelineViewModel"/>.
///
/// Interactions: click-drag a keyframe to move it in time (snaps); click empty track to set
/// playhead; Cut/Trim buttons operate on the playhead-centered selection. Keeps genuinely
/// light — NOT a multi-track NLE.</summary>
public sealed partial class TimelinePanel : UserControl
{
    public TimelineViewModel Vm { get; private set; } = null!;

    private double _dragOriginalTimeMs;
    private bool _isDraggingKeyframe;
    private double _selectionStartMs;
    private double _selectionEndMs;

    private const double TrackHeight = 60;
    private const double RulerHeight = 16;

    public TimelinePanel() { InitializeComponent(); }

    public void InitializeTimeline(TimelineViewModel vm)
    {
        Vm = vm;
        Render();
        UpdateDurationText();
        Vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(Render);
    }

    private void UpdateDurationText()
    {
        DurationText.Text = $"{Vm.KeptDurationMs / 1000:F1}s kept / {Vm.DurationMs / 1000:F1}s total";
    }

    /// <summary>Render the ruler + segments + keyframes + playhead onto the Canvas.</summary>
    private void Render()
    {
        TrackCanvas.Children.Clear();
        if (Vm.DurationMs <= 0) return;
        var width = TrackCanvas.ActualWidth > 0 ? TrackCanvas.ActualWidth : 600;
        var pxPerMs = width / Vm.DurationMs;

        // Time ruler ticks (every ~10%).
        for (int i = 0; i <= 10; i++)
        {
            var x = width * i / 10;
            var tick = new Rectangle { Width = 1, Height = RulerHeight, Fill = new SolidColorBrush(Microsoft.UI.Colors.DimGray) };
            Canvas.SetLeft(tick, x); Canvas.SetTop(tick, 0);
            TrackCanvas.Children.Add(tick);
            var label = new TextBlock { Text = $"{Vm.DurationMs * i / 10 / 1000:F1}s", FontSize = 9, Opacity = 0.5 };
            Canvas.SetLeft(label, x + 2); Canvas.SetTop(label, 0);
            TrackCanvas.Children.Add(label);
        }

        // Kept segments (bars).
        foreach (var seg in Vm.Segments)
        {
            var bar = new Rectangle
            {
                Width = Math.Max(2, seg.DurationMs * pxPerMs),
                Height = TrackHeight,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 88, 166, 255)),
                RadiusX = 3, RadiusY = 3,
            };
            Canvas.SetLeft(bar, seg.StartMs * pxPerMs);
            Canvas.SetTop(bar, RulerHeight + 4);
            TrackCanvas.Children.Add(bar);
        }

        // Keyframe dots (draggable).
        foreach (var k in Vm.Keyframes)
        {
            var dot = new Ellipse
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(k.Scale > 1 ? Microsoft.UI.Colors.Orange : Microsoft.UI.Colors.LightSkyBlue),
                Tag = k.TimeMs, // carry the original time for drag
            };
            ToolTipService.SetToolTip(dot, $"t={k.TimeMs:F0}ms scale={k.Scale:F1}");
            Canvas.SetLeft(dot, k.TimeMs * pxPerMs - 6);
            Canvas.SetTop(dot, RulerHeight + 4 + TrackHeight / 2 - 6);
            TrackCanvas.Children.Add(dot);
        }

        // Playhead (vertical line).
        var ph = new Rectangle { Width = 2, Height = RulerHeight + TrackHeight + 8, Fill = new SolidColorBrush(Microsoft.UI.Colors.Red) };
        Canvas.SetLeft(ph, Vm.PlayheadMs * pxPerMs);
        Canvas.SetTop(ph, 0);
        TrackCanvas.Children.Add(ph);

        UpdateDurationText();
        PlayheadText.Text = $"{Vm.PlayheadMs / 1000:F2}s";
    }

    private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var width = TrackCanvas.ActualWidth > 0 ? TrackCanvas.ActualWidth : 600;
        var x = e.GetCurrentPoint(TrackCanvas).Position.X;
        var tMs = Math.Clamp(x / width * Vm.DurationMs, 0, Vm.DurationMs);

        // If near a keyframe, start dragging it; else move playhead.
        var near = Vm.Keyframes.FirstOrDefault(k => Math.Abs(k.TimeMs - tMs) < Vm.DurationMs * 0.03);
        if (near.TimeMs != 0 || Vm.Keyframes.Count > 0 && Math.Abs(Vm.Keyframes[0].TimeMs - tMs) < Vm.DurationMs * 0.03)
        {
            _isDraggingKeyframe = true;
            _dragOriginalTimeMs = near.TimeMs;
        }
        else
        {
            Vm.PlayheadMs = tMs;
            _selectionStartMs = tMs;
            Render();
        }
        (sender as UIElement)?.CapturePointer(e.Pointer);
    }

    private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingKeyframe) return;
        var width = TrackCanvas.ActualWidth > 0 ? TrackCanvas.ActualWidth : 600;
        var x = e.GetCurrentPoint(TrackCanvas).Position.X;
        var tMs = Math.Clamp(x / width * Vm.DurationMs, 0, Vm.DurationMs);
        _selectionEndMs = tMs;
        Vm.PlayheadMs = tMs;
        Render();
    }

    private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingKeyframe)
        {
            var width = TrackCanvas.ActualWidth > 0 ? TrackCanvas.ActualWidth : 600;
            var x = e.GetCurrentPoint(TrackCanvas).Position.X;
            var newT = Math.Clamp(x / width * Vm.DurationMs, 0, Vm.DurationMs);
            // Re-find the original keyframe value, then move it.
            var orig = Vm.Keyframes.FirstOrDefault(k => Math.Abs(k.TimeMs - _dragOriginalTimeMs) < 0.5);
            if (orig.TimeMs != 0 || Vm.Keyframes.Count > 0)
                Vm.MoveKeyframe(_dragOriginalTimeMs, orig with { TimeMs = newT });
            _isDraggingKeyframe = false;
        }
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
    }

    private void OnCutClicked(object sender, RoutedEventArgs e)
    {
        // Cut a 1-second span around the playhead (demo of the Cut command).
        var from = Math.Max(0, Vm.PlayheadMs - 500);
        Vm.Cut(from, from + 1000);
    }

    private void OnTrimClicked(object sender, RoutedEventArgs e)
    {
        // Trim to keep from the playhead onward (demo of Trim).
        Vm.Trim(Vm.PlayheadMs, Vm.DurationMs);
    }
}
