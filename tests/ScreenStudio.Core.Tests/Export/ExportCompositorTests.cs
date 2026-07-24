using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Export;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.Core.Tests.Export;

public class ExportSettingsViewModelTests
{
    [Fact]
    public void Defaults_Are_Valid_And_Exportable()
    {
        var vm = new ExportSettingsViewModel { OutputPath = "out.mp4" };
        Assert.True(vm.CanExport);
        Assert.Empty(vm.ValidationError);
    }

    [Fact]
    public void Invalid_FrameRate_Disables_Export()
    {
        var vm = new ExportSettingsViewModel { OutputPath = "out.mp4", FrameRate = 45 };
        Assert.False(vm.CanExport);
        Assert.Contains("FrameRate", vm.ValidationError);
    }

    [Fact]
    public void Missing_Output_Path_Disables_Export()
    {
        var vm = new ExportSettingsViewModel { OutputPath = "" };
        Assert.False(vm.CanExport);
        Assert.Contains("Output path", vm.ValidationError);
    }

    [Fact]
    public void ApplyPreset_Sets_All_Properties()
    {
        var vm = new ExportSettingsViewModel { OutputPath = "out.mp4" };
        vm.ApplyPreset(ExportSettingsViewModel.Presets[3]); // 4K/60
        Assert.Equal(3840, vm.Width);
        Assert.Equal(2160, vm.Height);
        Assert.Equal(60, vm.FrameRate);
        Assert.True(vm.CanExport);
    }

    [Fact]
    public void PropertyChanged_Fires_On_Set()
    {
        var vm = new ExportSettingsViewModel();
        var fired = new List<string?>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName);

        vm.BitrateKbps = 9999;
        Assert.Contains(nameof(vm.BitrateKbps), fired);
        Assert.Equal(9999, vm.BitrateKbps);
    }

    [Fact]
    public void Presets_Are_All_Valid()
    {
        foreach (var p in ExportSettingsViewModel.Presets)
        {
            var vm = new ExportSettingsViewModel { OutputPath = "out.mp4" };
            vm.ApplyPreset(p);
            Assert.True(vm.CanExport, $"preset {p.Name} should be valid: {vm.ValidationError}");
        }
    }
}

public class FrameCompositorTests
{
    private static byte[] SolidFrame(int w, int h, byte b, byte g, byte r)
    {
        var buf = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            buf[i * 4 + 0] = b;
            buf[i * 4 + 1] = g;
            buf[i * 4 + 2] = r;
            buf[i * 4 + 3] = 255;
        }
        return buf;
    }

    [Fact]
    public void Scale_One_Copies_Source()
    {
        var src = SolidFrame(4, 4, 10, 20, 30);
        var cam = new CameraState(0, new(0.5, 0.5), 1.0); // no zoom, centered
        var out_ = FrameCompositor.Compose(src, 4, 4, cam, null, new(1, 1, false));
        // With scale=1 and centered focus, output == source.
        Assert.Equal(src, out_);
    }

    [Fact]
    public void Zoom_Centers_On_Focus_Region()
    {
        // Source: left half blue (B=255), right half red (R=255).
        var w = 8; var h = 8;
        var src = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (x < w / 2) { src[i + 0] = 255; src[i + 1] = 0; src[i + 2] = 0; }   // blue
                else           { src[i + 0] = 0; src[i + 1] = 0; src[i + 2] = 255; }   // red
                src[i + 3] = 255;
            }

        // Zoom 2x on the left half (focus at x=0.25).
        var cam = new CameraState(0, new(0.25, 0.5), 2.0);
        var out_ = FrameCompositor.Compose(src, w, h, cam, null, new(1, 1, false));

        // Center of the output should now be blue (we zoomed into the left/blue region).
        int center = (h / 2 * w + w / 2) * 4;
        Assert.Equal(255, out_[center + 0]); // B channel
        Assert.Equal(0, out_[center + 2]);   // R channel
    }

    [Fact]
    public void Cursor_Overlay_Draws_At_Position()
    {
        var src = SolidFrame(20, 20, 0, 0, 0); // black
        var cam = new CameraState(0, new(0.5, 0.5), 1.0);
        var cursor = new CursorEvent(0, new Vec2(10, 10), CursorButtonState.None);
        var out_ = FrameCompositor.Compose(src, 20, 20, cam, cursor, new(1, 1.0, true));

        // Pixel at (10,10) should now be bright (cursor drawn there).
        int idx = (10 * 20 + 10) * 4;
        var brightness = out_[idx + 0] + out_[idx + 1] + out_[idx + 2];
        Assert.True(brightness > 600, $"cursor pixel too dark: {brightness}");
    }

    [Fact]
    public void Hidden_Cursor_Not_Drawn()
    {
        var src = SolidFrame(20, 20, 0, 0, 0);
        var cam = new CameraState(0, new(0.5, 0.5), 1.0);
        var cursor = new CursorEvent(0, new Vec2(10, 10), CursorButtonState.None);
        var out_ = FrameCompositor.Compose(src, 20, 20, cam, cursor, new(1, 1.0, Visible: false));
        // Cursor hidden: the cursor pixel stays black (background), alpha 255.
        int idx = (10 * 20 + 10) * 4;
        Assert.Equal(0, out_[idx + 0]); // B
        Assert.Equal(0, out_[idx + 1]); // G
        Assert.Equal(0, out_[idx + 2]); // R
        Assert.Equal(255, out_[idx + 3]); // A
    }

    [Fact]
    public void Too_Small_Source_Buffer_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            FrameCompositor.Compose(new byte[10], 4, 4, new CameraState(0, new(0.5, 0.5), 1), null, default));
    }
}

public class FrameCompositorWebcamTests
{
    private static byte[] SolidFrame(int w, int h, byte b, byte g, byte r)
    {
        var buf = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            buf[i * 4 + 0] = b;
            buf[i * 4 + 1] = g;
            buf[i * 4 + 2] = r;
            buf[i * 4 + 3] = 255;
        }
        return buf;
    }

    [Fact]
    public void Webcam_Overlay_Draws_Into_Inset_Region()
    {
        // Black source frame; white webcam frame. After compositing the PiP inset must be
        // bright while the area outside the inset stays black.
        const int W = 100, H = 100;
        var src = SolidFrame(W, H, 0, 0, 0);
        var webcam = SolidFrame(20, 20, 255, 255, 255); // white
        var cam = new CameraState(0, new(0.5, 0.5), 1.0);

        // Inset covering the top-left quarter: x[0,25), y[0,25) in a 100x100 frame.
        var overlay = new WebcamOverlay(0, 0, 0.25, 0.25, 1.0);
        var out_ = FrameCompositor.ComposeWithWebcam(
            src, W, H, cam, null, new(1, 1, false),
            webcam, 20, 20, overlay);

        // Inside the inset (10,10): should be bright (white webcam alpha-over black).
        int inside = (10 * W + 10) * 4;
        var insideBrightness = out_[inside + 0] + out_[inside + 1] + out_[inside + 2];
        Assert.True(insideBrightness > 600, $"inset pixel should be bright, got {insideBrightness}");

        // Outside the inset (50,50): should remain black.
        int outside = (50 * W + 50) * 4;
        Assert.Equal(0, out_[outside + 0]);
        Assert.Equal(0, out_[outside + 1]);
        Assert.Equal(0, out_[outside + 2]);
        Assert.Equal(255, out_[outside + 3]);
    }

    [Fact]
    public void Webcam_Overlay_Insets_Are_Clamped_To_Frame()
    {
        // An inset that runs past the right/bottom edge must not write out of bounds — the
        // compositor clamps the rectangle rather than overflowing the BGRA buffer.
        const int W = 40, H = 40;
        var src = SolidFrame(W, H, 0, 0, 0);
        var webcam = SolidFrame(8, 8, 200, 200, 200);
        var cam = new CameraState(0, new(0.5, 0.5), 1.0);
        // 90% width starting at 50% -> extends well past the right edge.
        var overlay = new WebcamOverlay(0.5, 0.5, 0.9, 0.9, 1.0);

        // Must not throw (IndexOutOfRange) and must still draw in the clamped region.
        var out_ = FrameCompositor.ComposeWithWebcam(
            src, W, H, cam, null, new(1, 1, false),
            webcam, 8, 8, overlay);
        // Pixel at the start of the inset (21,21) should be bright.
        int idx = (21 * W + 21) * 4;
        Assert.True(out_[idx + 0] > 100, "clamped inset did not draw");
    }

    [Fact]
    public void Zero_Opacity_Leaves_Output_Unchanged()
    {
        const int W = 16, H = 16;
        var src = SolidFrame(W, H, 10, 20, 30);
        var webcam = SolidFrame(4, 4, 255, 255, 255);
        var cam = new CameraState(0, new(0.5, 0.5), 1.0);
        var overlay = new WebcamOverlay(0, 0, 0.5, 0.5, Opacity: 0.0);

        var out_ = FrameCompositor.ComposeWithWebcam(
            src, W, H, cam, null, new(1, 1, false),
            webcam, 4, 4, overlay);

        // Opacity 0 -> no webcam drawn -> output equals the plain Compose result.
        var baseline = FrameCompositor.Compose(src, W, H, cam, null, new(1, 1, false));
        Assert.Equal(baseline, out_);
    }

    [Fact]
    public void Too_Small_Webcam_Buffer_Throws()
    {
        var output = SolidFrame(10, 10, 0, 0, 0);
        Assert.Throws<ArgumentException>(() =>
            FrameCompositor.CompositeWebcam(
                output, 10, 10, new byte[5], 4, 4, WebcamOverlay.BottomRight));
    }
}
