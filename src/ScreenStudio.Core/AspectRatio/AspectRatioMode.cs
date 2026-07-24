using ScreenStudio.Core.Zoom;
using Vec2 = ScreenStudio.Core.Math.Vec2;

namespace ScreenStudio.Core.AspectRatio;

/// <summary>An output aspect-ratio mode (task 009). Width:Height as a reduced ratio.</summary>
public readonly record struct AspectRatioMode(int Width, int Height, string Label)
{
    public double Ratio => Width / (double)Height;

    public static readonly AspectRatioMode Landscape = new(16, 9, "16:9 Landscape");
    public static readonly AspectRatioMode Portrait  = new(9, 16, "9:16 Portrait");

    /// <summary>Social media presets (PRD P1: TikTok / IG Reels / YouTube Shorts).</summary>
    public static readonly AspectRatioMode TikTok         = new(9, 16, "TikTok (9:16)");
    public static readonly AspectRatioMode InstagramReel = new(9, 16, "Instagram Reels (9:16)");
    public static readonly AspectRatioMode YouTubeShort  = new(9, 16, "YouTube Shorts (9:16)");
    public static readonly AspectRatioMode Square        = new(1, 1,  "Square (1:1)");
}

/// <summary>Task 009 — aspect-ratio recalculation. Because ZoomKeyframes store focus as a
/// RELATIVE position (0..1 of the source), switching aspect ratio only needs to re-map the
/// relative focus through the new viewport's normalization and keep the content centered.
/// This delivers "auto-adjust all zooms when switching modes" + social presets.</summary>
public static class AspectRatioConverter
{
    /// <summary>Re-express a zoom program's relative focus coordinates for a new aspect ratio.
    ///
    /// Model: a keyframe's RelativeFocus is in [0,1] relative to the SOURCE frame. When we
    /// switch aspect ratio, the visible content area becomes the largest target-aspect
    /// rectangle fitting inside the source (FitInto). We re-express the focus relative to
    /// THAT content rectangle, then shift it so the content stays centered in the output.
    /// This delivers "auto-adjust all zooms when switching modes".</summary>
    public static ZoomProgram Convert(ZoomProgram source, AspectRatioMode target, Vec2 sourceSize)
    {
        var result = new ZoomProgram();
        var content = FitInto(sourceSize, target);

        // Offset of the content rectangle inside the source (centered framing).
        var offX = (sourceSize.X - content.X) / 2.0;
        var offY = (sourceSize.Y - content.Y) / 2.0;

        foreach (var k in source.Keyframes)
        {
            // Source-relative focus -> source pixels.
            var absX = k.RelativeFocus.X * sourceSize.X;
            var absY = k.RelativeFocus.Y * sourceSize.Y;
            // -> position within the content rectangle -> relative to content.
            var newRel = new Vec2(
                content.X > 0 ? (absX - offX) / content.X : 0.5,
                content.Y > 0 ? (absY - offY) / content.Y : 0.5);
            newRel = Clamp01(newRel);
            result.Add(k with { RelativeFocus = newRel });
        }
        return result;
    }

    /// <summary>Largest target-aspect rectangle that fits inside the source (centered).
    /// Represents the visible content area after any letterboxing/pillarboxing.</summary>
    public static Vec2 FitInto(Vec2 sourceSize, AspectRatioMode mode)
    {
        var srcRatio = sourceSize.X / sourceSize.Y;
        var dstRatio = mode.Ratio;
        if (dstRatio > srcRatio)
            return new(sourceSize.X, sourceSize.X / dstRatio); // letterbox top/bottom
        return new(sourceSize.Y * dstRatio, sourceSize.Y);      // pillarbox left/right
    }

    private static Vec2 Clamp01(Vec2 v)
        => new(System.Math.Clamp(v.X, 0, 1), System.Math.Clamp(v.Y, 0, 1));
}
