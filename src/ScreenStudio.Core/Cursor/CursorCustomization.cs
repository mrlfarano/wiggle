using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using SysMath = System.Math;

namespace ScreenStudio.Core.Cursor;

/// <summary>Task 007 — cursor customization settings. Drives the cursor-customization
/// panel UI and the per-frame cursor render style consumed by the export pipeline.
///
/// Deliverables covered:
///  - Cursor customization panel (this settings object)
///  - Size slider with presets (SizePresets)
///  - Auto-hide toggle and timeout (AutoHideEnabled / AutoHideTimeoutMs)
///  - Loop position option (LoopEnabled + the LoopClosingPath helper)
///  - Custom cursor image support (CustomImagePath)</summary>
public sealed class CursorCustomization
{
    /// <summary>Multiplier on the default system cursor size (1.0 = native).</summary>
    public double SizeMultiplier { get; set; } = 1.0;

    /// <summary>When true, a static (non-moving) cursor fades out after the timeout.</summary>
    public bool AutoHideEnabled { get; set; } = false;

    /// <summary>Static-cursor dwell time before fade-out begins.</summary>
    public double AutoHideTimeoutMs { get; set; } = 1500.0;

    /// <summary>When true, the cursor path is closed into a loop for seamless looping videos.</summary>
    public bool LoopEnabled { get; set; } = false;

    /// <summary>Optional path to a custom cursor image (PNG with alpha). Null = system cursor.</summary>
    public string? CustomImagePath { get; set; }

    public static readonly double[] SizePresets = { 0.75, 1.0, 1.5, 2.0, 3.0 };

    public void Validate()
    {
        if (SizeMultiplier <= 0 || SizeMultiplier > 10)
            throw new InvalidOperationException("SizeMultiplier must be in (0, 10]");
        if (AutoHideTimeoutMs < 0)
            throw new InvalidOperationException("AutoHideTimeoutMs must be >= 0");
    }

    /// <summary>Resolve the render style for a frame given how long the cursor has been idle.</summary>
    public CursorRenderStyle ToRenderStyle(double idleMs)
    {
        var opacity = 1.0;
        if (AutoHideEnabled && idleMs > AutoHideTimeoutMs)
        {
            // 300ms ease-out fade after the timeout.
            var fade = SysMath.Clamp((idleMs - AutoHideTimeoutMs) / 300.0, 0, 1);
            opacity = 1.0 - fade;
        }
        var visible = opacity > 0.01;
        return new CursorRenderStyle(SizeMultiplier, opacity, visible);
    }
}

/// <summary>Loop-position helper: produces a closing cursor path so the clip can loop
/// seamlessly (PRD P1: "Loop cursor position for seamless looping videos").</summary>
public static class CursorLoop
{
    /// <summary>Append interpolated samples that move the cursor from the last recorded
    /// position back toward the first, over the given duration. Uses simple linear blend;
    /// the smoother can re-smooth the combined stream.</summary>
    public static List<CursorEvent> AppendReturnPath(
        IReadOnlyList<CursorEvent> source, double returnDurationMs, double frameRateHz = 60.0)
    {
        if (source.Count < 2) return new List<CursorEvent>(source);
        var result = new List<CursorEvent>(source);
        var start = source[^1];
        var end = source[0];
        var steps = SysMath.Max(1, (int)(returnDurationMs / 1000.0 * frameRateHz));
        var dt = returnDurationMs / steps;
        for (int i = 1; i <= steps; i++)
        {
            var u = (double)i / steps;
            var p = start.Position.Lerp(end.Position, u);
            result.Add(new CursorEvent(start.TimestampMs + i * dt, p, CursorButtonState.None));
        }
        return result;
    }
}

/// <summary>Render style for the cursor overlay (consumed by the export pipeline).</summary>
public readonly record struct CursorRenderStyle(double SizeMultiplier, double Opacity, bool Visible);
