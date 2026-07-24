using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Tests.Cursor;

public class CursorCustomizationTests
{
    [Fact]
    public void Size_Presets_Available_And_Valid()
    {
        Assert.True(CursorCustomization.SizePresets.Length >= 3);
        Assert.All(CursorCustomization.SizePresets, s => Assert.InRange(s, 0.1, 10));
        foreach (var s in CursorCustomization.SizePresets)
            new CursorCustomization { SizeMultiplier = s }.Validate();
    }

    [Fact]
    public void AutoHide_Fades_After_Timeout()
    {
        var c = new CursorCustomization { AutoHideEnabled = true, AutoHideTimeoutMs = 1000 };

        // Before timeout -> fully visible.
        Assert.Equal(1.0, c.ToRenderStyle(500).Opacity, 6);
        Assert.True(c.ToRenderStyle(500).Visible);

        // Exactly at timeout -> still visible.
        Assert.Equal(1.0, c.ToRenderStyle(1000).Opacity, 6);

        // Mid-fade (300ms after timeout => halfway).
        Assert.Equal(0.5, c.ToRenderStyle(1150).Opacity, 2);

        // Fully faded.
        var faded = c.ToRenderStyle(2000);
        Assert.Equal(0.0, faded.Opacity, 6);
        Assert.False(faded.Visible);
    }

    [Fact]
    public void AutoHide_Disabled_Always_Full_Opacity()
    {
        var c = new CursorCustomization { AutoHideEnabled = false };
        Assert.Equal(1.0, c.ToRenderStyle(999_999).Opacity, 6);
        Assert.True(c.ToRenderStyle(0).Visible);
    }

    [Fact]
    public void Size_Multiplier_Passes_Through()
    {
        var c = new CursorCustomization { SizeMultiplier = 2.5 };
        Assert.Equal(2.5, c.ToRenderStyle(0).SizeMultiplier);
    }

    [Fact]
    public void Loop_Return_Path_Connects_Last_To_First()
    {
        var src = new List<CursorEvent>
        {
            new(0,    new(0, 0),   CursorButtonState.None),
            new(1000, new(100, 0), CursorButtonState.None),
        };
        var looped = CursorLoop.AppendReturnPath(src, returnDurationMs: 1000, frameRateHz: 60);

        // Last appended sample must land at (or extremely near) the first position (0,0).
        Assert.True(looped.Count > src.Count);
        var last = looped[^1];
        Assert.Equal(0.0, last.Position.X, 4);
        Assert.Equal(0.0, last.Position.Y, 4);

        // And the timestamps continue forward from the source end.
        Assert.True(last.TimestampMs > src[^1].TimestampMs);
    }

    [Fact]
    public void Validate_Rejects_Bad_Values()
    {
        Assert.Throws<InvalidOperationException>(() => new CursorCustomization { SizeMultiplier = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new CursorCustomization { SizeMultiplier = 100 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new CursorCustomization { AutoHideTimeoutMs = -1 }.Validate());
    }
}
