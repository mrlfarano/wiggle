using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Export;

/// <summary>PRD P2 #7 — "Crop recording area." A crop region in source pixels: only the
/// interior rectangle is included in the output. Applied before the zoom transform.</summary>
public readonly record struct CropRegion(int X, int Y, int Width, int Height)
{
    /// <summary>No crop — the full source frame.</summary>
    public static readonly CropRegion None = new(0, 0, 0, 0);
    public bool IsNone => Width <= 0 || Height <= 0;
}

/// <summary>Extension to FrameCompositor for cropping. Crops a source frame to the region
/// before compositing. Pure function, headless-testable.</summary>
public static class CropCompositor
{
    /// <summary>Crop a BGRA source frame to the given region. Returns a new buffer containing
    /// only the cropped rectangle. If the region is None, returns the source unchanged.</summary>
    public static byte[] Crop(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, CropRegion region)
    {
        if (region.IsNone) return source.ToArray();

        var cropW = System.Math.Min(region.Width, sourceWidth - region.X);
        var cropH = System.Math.Min(region.Height, sourceHeight - region.Y);
        var output = new byte[cropW * cropH * 4];

        for (int y = 0; y < cropH; y++)
        {
            var srcRow = ((region.Y + y) * sourceWidth + region.X) * 4;
            var dstRow = y * cropW * 4;
            source.Slice(srcRow, cropW * 4).CopyTo(output.AsSpan(dstRow));
        }
        return output;
    }
}
