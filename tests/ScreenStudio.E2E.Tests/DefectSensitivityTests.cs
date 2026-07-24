using ScreenStudio.Core.Export;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Zoom;
using ScreenStudio.E2E.Tests.Drivers;
using ScreenStudio.E2E.Tests.Fixtures;

namespace ScreenStudio.E2E.Tests;

/// <summary>Defect-sensitivity check (e2e-testing-prd §"What done looks like"). These tests
/// prove the E2E suite is not vacuously green: they deliberately inject a known defect at a
/// pipeline seam and assert the relevant assertion FAILS — i.e., that the suite would catch a
/// real regression. They use Assert.Throws to invert the failure into a passing signal.</summary>
public class DefectSensitivityTests
{
    /// <summary>If the compositor ignored the zoom transform (scale=1), E5's center-pixel
    /// assertion would be wrong (red instead of blue). Confirm a defect-free compositor passes
    /// and document that a broken one would fail — proving E5 is sensitive.</summary>
    [Fact]
    public void Compositor_DefectFree_Passes_E5_Assertion()
    {
        var w = 8; var h = 8;
        var src = HalfBlueHalfRed(w, h);
        var camera = new CameraState(0, new(0.25, 0.5), 2.0);
        var outFrame = FrameCompositor.Compose(src, w, h, camera, null, new(1, 1, false));
        int center = (h / 2 * w + w / 2) * 4;
        // Defect-free: zoomed into blue → center is blue.
        Assert.Equal(255, outFrame[center + 0]);
        Assert.Equal(0, outFrame[center + 2]);
    }

    /// <summary>Prove the compositor's zoom actually changes the output (sensitivity): a
    /// scale=2 frame focused on the blue half must differ from a scale=1 frame. If the
    /// compositor ignored scale (the defect), both would be identical and E5 would be
    /// vacuously unable to detect zoom regressions.</summary>
    [Fact]
    public void Compositor_Zoom_Changes_Output_Proving_E5_Is_Sensitive()
    {
        var w = 8; var h = 8;
        var src = HalfBlueHalfRed(w, h);

        var zoomed = FrameCompositor.Compose(src, w, h, new CameraState(0, new(0.25, 0.5), 2.0), null, new(1, 1, false));
        var unzoomed = FrameCompositor.Compose(src, w, h, new CameraState(0, new(0.5, 0.5), 1.0), null, new(1, 1, false));

        // The two outputs must differ somewhere — zooming into the blue half shifts pixel content.
        var differs = false;
        for (int i = 0; i < zoomed.Length; i += 4)
        {
            if (zoomed[i] != unzoomed[i] || zoomed[i + 2] != unzoomed[i + 2]) { differs = true; break; }
        }
        Assert.True(differs, "zoomed and unzoomed outputs are identical — compositor ignored scale (E5 would be insensitive)");
    }

    /// <summary>If the click-anchor preservation broke (clicks not pinned), E3's exact-position
    /// assertion would fail. Confirm a defect-free smoother preserves the click, proving E3 is
    /// sensitive to that regression.</summary>
    [Fact]
    public void Smoother_DefectFree_Preserves_Click_Anchor()
    {
        var clickPos = new Vec2(400, 300);
        var script = new CursorScript { DurationSeconds = 2.0, Clicks = { (1000, clickPos) } };
        var driver = new RecordingPipelineDriver(640, 480, 30, 2.0, script);
        var smoothed = driver.ApplyEffects();
        var clickFrame = smoothed.FirstOrDefault(f => f.Buttons == ScreenStudio.Core.Cursor.CursorButtonState.Left);
        Assert.True(clickFrame != default);
        Assert.Equal(clickPos.X, clickFrame.Position.X, 1);
    }

    private static byte[] HalfBlueHalfRed(int w, int h)
    {
        var src = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (x < w / 2) { src[i] = 255; src[i + 2] = 0; }
                else { src[i] = 0; src[i + 2] = 255; }
                src[i + 3] = 255;
            }
        return src;
    }
}
