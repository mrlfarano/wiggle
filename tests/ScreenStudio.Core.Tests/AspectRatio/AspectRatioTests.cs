using ScreenStudio.Core.AspectRatio;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.Core.Tests.AspectRatio;

public class AspectRatioTests
{
    private static readonly Vec2 Source = new(1920, 1080);

    [Fact]
    public void Presets_Have_Correct_Ratios()
    {
        Assert.Equal(16 / 9.0, AspectRatioMode.Landscape.Ratio, 4);
        Assert.Equal(9 / 16.0, AspectRatioMode.Portrait.Ratio, 4);
        Assert.Equal(1.0, AspectRatioMode.Square.Ratio, 4);
    }

    [Fact]
    public void FitInto_Landscape_Source_To_Portrait_Pillarboxes()
    {
        // 16:9 source -> 9:16 target: width shrinks, height stays full.
        var fit = AspectRatioConverter.FitInto(Source, AspectRatioMode.Portrait);
        Assert.Equal(1080, fit.Y, 1);                 // full height
        Assert.Equal(607.5, fit.X, 1);                // 1080 * 9/16
    }

    [Fact]
    public void Convert_Preserves_Focus_As_Relative_And_Clamps()
    {
        var prog = new ZoomProgram()
            .Add(new ZoomKeyframe(0, new(0.5, 0.5), 2.0))
            .Add(new ZoomKeyframe(1000, new(0.25, 0.75), 2.5));

        var portrait = AspectRatioConverter.Convert(prog, AspectRatioMode.Portrait, Source);

        // Centered focus (0.5,0.5) maps back to roughly centered in the new framing.
        Assert.Equal(0.5, portrait.Keyframes[0].RelativeFocus.X, 2);
        Assert.Equal(0.5, portrait.Keyframes[0].RelativeFocus.Y, 2);

        // Scale is unchanged by aspect conversion.
        Assert.Equal(2.0, portrait.Keyframes[0].Scale);
        Assert.Equal(2.5, portrait.Keyframes[1].Scale);

        // All relative coords stay in [0,1].
        foreach (var k in portrait.Keyframes)
        {
            Assert.InRange(k.RelativeFocus.X, 0, 1);
            Assert.InRange(k.RelativeFocus.Y, 0, 1);
        }
    }

    [Fact]
    public void Convert_Roundtrips_Through_Landscape()
    {
        // Source is already 16:9 (Landscape). Converting to Landscape is identity.
        var prog = new ZoomProgram()
            .Add(new ZoomKeyframe(0, new(0.3, 0.4), 2.0));
        var same = AspectRatioConverter.Convert(prog, AspectRatioMode.Landscape, Source);
        Assert.Equal(0.3, same.Keyframes[0].RelativeFocus.X, 2);
        Assert.Equal(0.4, same.Keyframes[0].RelativeFocus.Y, 2);
    }

    [Fact]
    public void One_Click_Switch_Produces_New_Program()
    {
        // Deliverable: "one-click mode switching" => Convert returns a new program
        // distinct from the source, with the same keyframe count.
        var prog = new ZoomProgram()
            .Add(new ZoomKeyframe(0, new(0.5, 0.5), 1.0))
            .Add(new ZoomKeyframe(500, new(0.6, 0.6), 2.0));
        var switched = AspectRatioConverter.Convert(prog, AspectRatioMode.Portrait, Source);
        Assert.Equal(prog.Count, switched.Count);
        Assert.NotSame(prog, switched);
    }
}
