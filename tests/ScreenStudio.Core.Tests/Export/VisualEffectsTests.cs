using ScreenStudio.Core.Export;
using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Tests.Export;

/// <summary>Tests for #017 (Visual customization) and #018 (Motion blur).</summary>
public class VisualFrameTests
{
    [Fact]
    public void ApplyVisualFrame_Fills_Background()
    {
        var content = new byte[4 * 4 * 4]; // 4x4 white
        Array.Fill(content, (byte)255);
        var frame = new FrameCompositor.VisualFrame(0x2e, 0x1a, 0x1a, 2, 0, 0, 0, 0, 0, 0, 0);

        var output = FrameCompositor.ApplyVisualFrame(content, 4, 4, 12, 12, frame);

        // Corner pixel (outside padding) should be the background color.
        int idx = 0; // (0,0)
        Assert.Equal(0x2e, output[idx + 0]); // B
        Assert.Equal(0x1a, output[idx + 1]); // G
        Assert.Equal(0x1a, output[idx + 2]); // R
    }

    [Fact]
    public void ApplyVisualFrame_Centers_Content_In_Padding()
    {
        // 4x4 content, 12x12 output, 4px padding → content fills the 4x4 interior exactly.
        var content = new byte[4 * 4 * 4];
        for (int i = 0; i < content.Length; i += 4) { content[i + 2] = 255; } // red
        var frame = FrameCompositor.VisualFrame.Default with { PaddingPx = 4 };

        var output = FrameCompositor.ApplyVisualFrame(content, 4, 4, 12, 12, frame);

        // Center pixel (6,6) should be content (red).
        int center = (6 * 12 + 6) * 4;
        Assert.Equal(255, output[center + 2]); // R = red content
    }

    [Fact]
    public void ApplyVisualFrame_Draws_Border()
    {
        var content = new byte[4 * 4 * 4];
        Array.Fill(content, (byte)255);
        var frame = new FrameCompositor.VisualFrame(0, 0, 0, 4, 0, 0, 0, 2, 0, 255, 0); // 2px green border

        var output = FrameCompositor.ApplyVisualFrame(content, 4, 4, 12, 12, frame);

        // A border pixel should have green channel set. The content is 4x4 centered in 12x12
        // with 4px padding → content at (4,4)-(8,8). Border at x=2 (just outside content).
        // Check a pixel at the border region — find any pixel with G>0 and B=0,R=0.
        var hasBorderPixel = false;
        for (int i = 0; i < output.Length; i += 4)
        {
            if (output[i + 1] > 200 && output[i + 0] == 0 && output[i + 2] == 0) { hasBorderPixel = true; break; }
        }
        Assert.True(hasBorderPixel, "no green border pixels found");
    }

    [Fact]
    public void ApplyVisualFrame_Zero_Padding_Fills_Entire_Output()
    {
        var content = new byte[2 * 2 * 4];
        for (int i = 0; i < content.Length; i += 4) { content[i] = 255; } // blue
        var frame = FrameCompositor.VisualFrame.Default with { PaddingPx = 0 };

        var output = FrameCompositor.ApplyVisualFrame(content, 2, 2, 4, 4, frame);

        // With zero padding, content scales to fill the entire 4x4 output.
        Assert.Equal(255, output[0]); // B = blue content
    }
}

public class MotionBlurTests
{
    [Fact]
    public void DrawMotionBlur_Creates_Trail_Between_Positions()
    {
        // 50x50 black frame; draw a trail from (10,25) to (40,25).
        var buf = new byte[50 * 50 * 4]; // all black
        var positions = new List<(Vec2 position, double ageMs)>
        {
            (new(10, 25), 0),
            (new(20, 25), 50),
            (new(30, 25), 100),
            (new(40, 25), 150),
        };

        FrameCompositor.DrawMotionBlur(buf, 50, 50, positions, intensity: 1.0, cursorRadius: 4, baseOpacity: 1.0);

        // The trail should have brightened pixels between the start and end positions.
        var brightCount = 0;
        for (int i = 0; i < buf.Length; i += 4)
        {
            if (buf[i] > 0 || buf[i + 1] > 0 || buf[i + 2] > 0) brightCount++;
        }
        Assert.True(brightCount > 10, $"expected trail pixels, got {brightCount}");
    }

    [Fact]
    public void DrawMotionBlur_Zero_Intensity_Draws_Nothing()
    {
        var buf = new byte[20 * 20 * 4];
        var positions = new List<(Vec2 position, double ageMs)>
        {
            (new(5, 10), 0), (new(15, 10), 100),
        };

        FrameCompositor.DrawMotionBlur(buf, 20, 20, positions, intensity: 0, cursorRadius: 3, baseOpacity: 1.0);

        // All pixels should remain zero (no trail drawn).
        Assert.All(buf, b => Assert.Equal(0, b));
    }

    [Fact]
    public void DrawMotionBlur_Single_Position_Draws_Nothing()
    {
        var buf = new byte[20 * 20 * 4];
        var positions = new List<(Vec2 position, double ageMs)> { (new(10, 10), 0) };

        FrameCompositor.DrawMotionBlur(buf, 20, 20, positions, intensity: 1.0, cursorRadius: 3, baseOpacity: 1.0);

        // Need >= 2 positions for a trail.
        Assert.All(buf, b => Assert.Equal(0, b));
    }
}
