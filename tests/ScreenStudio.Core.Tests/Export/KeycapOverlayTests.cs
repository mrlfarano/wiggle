using ScreenStudio.Core.Export;

namespace ScreenStudio.Core.Tests.Export;

/// <summary>Tests for #019 — keycap overlay rendering.</summary>
public class KeycapOverlayTests
{
    [Fact]
    public void DrawKeycap_Produces_Visible_Pixels()
    {
        var buf = new byte[100 * 60 * 4]; // black 100x60
        var keycap = new FrameCompositor.KeycapDisplay(Text: "Ctrl+C", NormalizedX: 0.5, NormalizedY: 0.85, Opacity: 0.9);

        FrameCompositor.DrawKeycap(buf, 100, 60, keycap);

        // The keycap region should have brightened pixels (background + text).
        var brightCount = 0;
        for (int i = 0; i < buf.Length; i += 4)
        {
            if (buf[i] > 0 || buf[i + 1] > 0 || buf[i + 2] > 0) brightCount++;
        }
        Assert.True(brightCount > 20, $"keycap drew too few pixels: {brightCount}");
    }

    [Fact]
    public void DrawKeycap_Zero_Opacity_Draws_Nothing()
    {
        var buf = new byte[100 * 60 * 4];
        FrameCompositor.DrawKeycap(buf, 100, 60, new(Text: "A", Opacity: 0));
        Assert.All(buf, b => Assert.Equal(0, b));
    }

    [Fact]
    public void DrawKeycap_Empty_Text_Draws_Nothing()
    {
        var buf = new byte[100 * 60 * 4];
        FrameCompositor.DrawKeycap(buf, 100, 60, new(Text: "", Opacity: 1.0));
        Assert.All(buf, b => Assert.Equal(0, b));
    }

    [Fact]
    public void DrawKeycap_Positions_At_Normalized_Coords()
    {
        // 200x100 frame, keycap at (0.25, 0.5) → center at (50, 50).
        var buf = new byte[200 * 100 * 4];
        FrameCompositor.DrawKeycap(buf, 200, 100, new(Text: "X", NormalizedX: 0.25, NormalizedY: 0.5, Opacity: 1.0));

        // Find the centroid of bright pixels — should be near (50, 50).
        var sumX = 0.0; var sumY = 0.0; var count = 0;
        for (int y = 0; y < 100; y++)
            for (int x = 0; x < 200; x++)
            {
                int idx = (y * 200 + x) * 4;
                if (buf[idx] > 0 || buf[idx + 1] > 0 || buf[idx + 2] > 0)
                {
                    sumX += x; sumY += y; count++;
                }
            }
        Assert.True(count > 0);
        var centroidX = sumX / count;
        var centroidY = sumY / count;
        // Centroid should be near the target (50, 50), within the keycap size.
        Assert.InRange(centroidX, 20, 80);
        Assert.InRange(centroidY, 30, 70);
    }
}
