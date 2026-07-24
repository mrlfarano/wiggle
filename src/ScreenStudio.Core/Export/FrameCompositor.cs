using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Math;
using ScreenStudio.Core.Zoom;
using SysMath = System.Math;

namespace ScreenStudio.Core.Export;

/// <summary>Picture-in-picture webcam inset (PRD P1 #3). All coordinates are normalized to
/// the output frame (0..1) so the overlay survives output rescaling. <see cref="Opacity"/> is
/// applied during alpha-over compositing; 1.0 = fully opaque webcam.</summary>
public readonly record struct WebcamOverlay(
    double NormalizedX,
    double NormalizedY,
    double NormalizedWidth,
    double NormalizedHeight,
    double Opacity = 1.0)
{
    /// <summary>Conventional bottom-right inset: 25% of the output frame, with a 3% margin.</summary>
    public static readonly WebcamOverlay BottomRight = new(0.72, 0.72, 0.25, 0.25, 1.0);
}

/// <summary>Task 006 — CPU RGB32 frame compositor. Takes a source frame (BGRA bytes) and
/// composes the final output frame by applying the zoom camera transform (crop + scale to the
/// focus region) and drawing the cursor overlay. This is the headless-testable core of the
/// "compose final frames from recording + effects" technical approach; the GPU/Direct2D path
/// in ScreenStudio.Native is the production renderer, but this CPU path is fully verifiable
/// and is what the export pipeline's tests use.</summary>
public static class FrameCompositor
{
    /// <summary>Compose one output frame.
    /// <param name="source">Source frame BGRA, width*height*4 bytes, row-major top-down.</param>
    /// <param name="sourceWidth/sourceHeight">Source dimensions.</param>
    /// <param name="camera">Resolved camera (focus in source-relative 0..1, scale).</param>
    /// <param name="cursor">Cursor position in source pixels (or null to skip overlay).</param>
    /// <param name="cursorStyle">Cursor render style (size multiplier, opacity, visibility).</param>
    /// <returns>The composed output frame (BGRA) at the source resolution.</returns>
    public static byte[] Compose(
        ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight,
        CameraState camera, CursorEvent? cursor, CursorRenderStyle cursorStyle)
    {
        if (source.Length < sourceWidth * sourceHeight * 4)
            throw new ArgumentException("source buffer too small", nameof(source));

        var output = new byte[sourceWidth * sourceHeight * 4];

        // Focus in absolute source pixels.
        var focusX = camera.Focus.X * sourceWidth;
        var focusY = camera.Focus.Y * sourceHeight;
        var scale = SysMath.Max(1.0, camera.Scale); // zoom >= 1

        // The visible window: a sourceWidth/scale × sourceHeight/scale region centered on focus.
        var halfWinW = sourceWidth / (2.0 * scale);
        var halfWinH = sourceHeight / (2.0 * scale);
        var winLeft = focusX - halfWinW;
        var winTop = focusY - halfWinH;

        for (int dy = 0; dy < sourceHeight; dy++)
        {
            for (int dx = 0; dx < sourceWidth; dx++)
            {
                // Map output pixel back to source pixel via the inverse zoom transform.
                var sx = winLeft + dx / scale;
                var sy = winTop + dy / scale;
                var sxi = (int)SysMath.Clamp(sx, 0, sourceWidth - 1);
                var syi = (int)SysMath.Clamp(sy, 0, sourceHeight - 1);

                int srcIdx = (syi * sourceWidth + sxi) * 4;
                int dstIdx = (dy * sourceWidth + dx) * 4;
                output[dstIdx + 0] = source[srcIdx + 0];
                output[dstIdx + 1] = source[srcIdx + 1];
                output[dstIdx + 2] = source[srcIdx + 2];
                output[dstIdx + 3] = 255; // opaque background
            }
        }

        // Cursor overlay (simple filled circle scaled by SizeMultiplier).
        if (cursorStyle.Visible && cursor is { } c)
        {
            // Transform cursor source position into output space.
            var cxOut = (c.Position.X - winLeft) * scale;
            var cyOut = (c.Position.Y - winTop) * scale;
            var radius = (int)(8 * cursorStyle.SizeMultiplier);
            DrawCursor(output, sourceWidth, sourceHeight, (int)cxOut, (int)cyOut, radius, cursorStyle.Opacity, c.Buttons);
        }

        return output;
    }

    /// <summary>Compose one output frame and alpha-blend the latest webcam frame into a
    /// picture-in-picture inset (PRD P1 #3). Equivalent to <see cref="Compose"/> followed by
    /// <see cref="CompositeWebcam"/>; the webcam is drawn on top of the cursor so the inset
    /// never gets partially erased by cursor rendering.</summary>
    public static byte[] ComposeWithWebcam(
        ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight,
        CameraState camera, CursorEvent? cursor, CursorRenderStyle cursorStyle,
        ReadOnlySpan<byte> webcam, int webcamWidth, int webcamHeight,
        WebcamOverlay overlay)
    {
        var output = Compose(source, sourceWidth, sourceHeight, camera, cursor, cursorStyle);
        CompositeWebcam(output, sourceWidth, sourceHeight, webcam, webcamWidth, webcamHeight, overlay);
        return output;
    }

    /// <summary>Alpha-blend a webcam frame into a picture-in-picture inset of an existing
    /// output frame (in-place). The inset rectangle is derived from <paramref name="overlay"/>'s
    /// normalized coordinates and the webcam is scaled into it with nearest-neighbor sampling
    /// (deliberately simple — the production GPU path uses bilinear; this CPU path is the
    /// testable reference implementation).</summary>
    /// <param name="output">BGRA output buffer to composite into, mutated in place. Dimensions
    /// <paramref name="outputWidth"/>×<paramref name="outputHeight"/>.</param>
    /// <param name="webcam">Webcam BGRA bytes, <paramref name="webcamWidth"/>×<paramref name="webcamHeight"/>×4.</param>
    public static void CompositeWebcam(
        byte[] output, int outputWidth, int outputHeight,
        ReadOnlySpan<byte> webcam, int webcamWidth, int webcamHeight,
        WebcamOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (webcamWidth <= 0 || webcamHeight <= 0) return;
        if (webcam.Length < webcamWidth * webcamHeight * 4)
            throw new ArgumentException("webcam buffer too small", nameof(webcam));
        if (output.Length < outputWidth * outputHeight * 4)
            throw new ArgumentException("output buffer too small", nameof(output));

        // Resolve the inset rectangle in output pixels and clamp to the frame.
        var rect = ResolveRect(overlay, outputWidth, outputHeight);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var a = SysMath.Clamp(overlay.Opacity, 0, 1);
        if (a <= 0) return;

        for (int dy = 0; dy < rect.Height; dy++)
        {
            // Nearest-neighbor source sample for this inset row/column.
            int sy = dy * webcamHeight / rect.Height;
            if (sy >= webcamHeight) sy = webcamHeight - 1;
            for (int dx = 0; dx < rect.Width; dx++)
            {
                int sx = dx * webcamWidth / rect.Width;
                if (sx >= webcamWidth) sx = webcamWidth - 1;
                int srcIdx = (sy * webcamWidth + sx) * 4;
                int dstIdx = ((rect.Y + dy) * outputWidth + (rect.X + dx)) * 4;
                // BGRA: webcam B,G,R blended over the existing output pixel.
                Blend(output, dstIdx,
                      webcam[srcIdx + 0], webcam[srcIdx + 1], webcam[srcIdx + 2], a);
                output[dstIdx + 3] = 255; // inset is opaque after compositing
            }
        }
    }

    private readonly record struct Rect(int X, int Y, int Width, int Height);

    private static Rect ResolveRect(WebcamOverlay o, int outW, int outH)
    {
        int x = (int)(o.NormalizedX * outW);
        int y = (int)(o.NormalizedY * outH);
        int w = (int)(o.NormalizedWidth * outW);
        int h = (int)(o.NormalizedHeight * outH);
        // Clamp to frame bounds so the inset never writes out of the buffer.
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x > outW) x = outW;
        if (y > outH) y = outH;
        if (x + w > outW) w = outW - x;
        if (y + h > outH) h = outH - y;
        return new Rect(x, y, w, h);
    }

    private static void DrawCursor(byte[] buf, int w, int h, int cx, int cy, int radius, double opacity, CursorButtonState buttons)
    {
        if (radius <= 0) return;
        var a = SysMath.Clamp(opacity, 0, 1);
        // White ring; red fill when clicking.
        byte cr = (byte)(buttons != CursorButtonState.None ? 255 : 255);
        byte cg = (byte)(buttons != CursorButtonState.None ? 40 : 255);
        byte cb = (byte)(buttons != CursorButtonState.None ? 40 : 255);

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > radius * radius) continue;
                int px = cx + x, py = cy + y;
                if (px < 0 || py < 0 || px >= w || py >= h) continue;
                int idx = (py * w + px) * 4;
                Blend(buf, idx, cb, cg, cr, a); // BGRA: B=cb, G=cg, R=cr
            }
        }
    }

    private static void Blend(byte[] buf, int idx, byte b, byte g, byte r, double a)
    {
        // Alpha-over compositing.
        buf[idx + 0] = (byte)(b * a + buf[idx + 0] * (1 - a));
        buf[idx + 1] = (byte)(g * a + buf[idx + 1] * (1 - a));
        buf[idx + 2] = (byte)(r * a + buf[idx + 2] * (1 - a));
    }

    // ---- Task #017: Visual customization (background, padding, shadow, border) ----

    /// <summary>Visual framing settings applied around the captured content.
    /// All sizes are in output pixels; colors are BGRA.</summary>
    public readonly record struct VisualFrame(
        byte BackgroundB, byte BackgroundG, byte BackgroundR,  // background fill color
        int PaddingPx,                                         // space around the content
        int ShadowPx,                                          // drop-shadow blur radius (0 = none)
        byte ShadowOpacity,                                    // shadow darkness
        int BorderRadiusPx,                                    // rounded-corner radius for the content
        int BorderPx,                                          // border thickness
        byte BorderB, byte BorderG, byte BorderR)             // border color
    {
        /// <summary>Default: solid dark background (#1a1a2e), 40px padding, no shadow/border.</summary>
        public static readonly VisualFrame Default = new(0x2e, 0x1a, 0x1a, 40, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>Apply visual framing (background + padding + optional shadow + border) around
    /// the composited content. Returns a new buffer at the same dimensions: the content is
    /// scaled into the padded interior and the border/background fills the rest.</summary>
    public static byte[] ApplyVisualFrame(
        ReadOnlySpan<byte> content, int contentWidth, int contentHeight,
        int outputWidth, int outputHeight, VisualFrame frame)
    {
        var output = new byte[outputWidth * outputHeight * 4];

        // Fill the background.
        for (int i = 0; i < output.Length; i += 4)
        {
            output[i + 0] = frame.BackgroundB;
            output[i + 1] = frame.BackgroundG;
            output[i + 2] = frame.BackgroundR;
            output[i + 3] = 255;
        }

        // Compute the content rectangle inside the padding.
        var pad = frame.PaddingPx;
        var interiorX = pad;
        var interiorY = pad;
        var interiorW = outputWidth - 2 * pad;
        var interiorH = outputHeight - 2 * pad;
        if (interiorW <= 0 || interiorH <= 0) return output; // padding eats everything

        // Scale content into the interior (nearest-neighbor, preserving aspect ratio).
        var scale = SysMath.Min((double)interiorW / contentWidth, (double)interiorH / contentHeight);
        var destW = (int)(contentWidth * scale);
        var destH = (int)(contentHeight * scale);
        var destX = interiorX + (interiorW - destW) / 2;
        var destY = interiorY + (interiorH - destH) / 2;

        // Optional drop shadow (simple offset + darkened halo).
        if (frame.ShadowPx > 0)
        {
            var shadowOffset = frame.ShadowPx / 2;
            for (int dy = -frame.ShadowPx; dy < destH + frame.ShadowPx; dy++)
            {
                for (int dx = -frame.ShadowPx; dx < destW + frame.ShadowPx; dx++)
                {
                    int px = destX + dx + shadowOffset;
                    int py = destY + dy + shadowOffset;
                    if (px < 0 || py < 0 || px >= outputWidth || py >= outputHeight) continue;
                    // Falloff: strongest at the content edge, fading outward.
                    var edgeDist = SysMath.Max(SysMath.Abs(dx < 0 ? dx : (dx >= destW ? dx - destW : 0)),
                                                SysMath.Abs(dy < 0 ? dy : (dy >= destH ? dy - destH : 0)));
                    var falloff = SysMath.Clamp(1.0 - (double)edgeDist / frame.ShadowPx, 0, 1);
                    var sa = (frame.ShadowOpacity / 255.0) * falloff * 0.5;
                    int idx = (py * outputWidth + px) * 4;
                    Blend(output, idx, 0, 0, 0, sa);
                }
            }
        }

        // Copy scaled content into the interior.
        for (int dy = 0; dy < destH; dy++)
        {
            int sy = dy * contentHeight / destH;
            if (sy >= contentHeight) sy = contentHeight - 1;
            for (int dx = 0; dx < destW; dx++)
            {
                int sx = dx * contentWidth / destW;
                if (sx >= contentWidth) sx = contentWidth - 1;
                int srcIdx = (sy * contentWidth + sx) * 4;
                int dstIdx = ((destY + dy) * outputWidth + (destX + dx)) * 4;
                output[dstIdx + 0] = content[srcIdx + 0];
                output[dstIdx + 1] = content[srcIdx + 1];
                output[dstIdx + 2] = content[srcIdx + 2];
                output[dstIdx + 3] = 255;
            }
        }

        // Optional border around the content rectangle.
        if (frame.BorderPx > 0)
        {
            DrawBorder(output, outputWidth, outputHeight, destX, destY, destW, destH, frame.BorderPx,
                       frame.BorderB, frame.BorderG, frame.BorderR);
        }

        return output;
    }

    /// <summary>Draw a rectangular border of the given thickness around a region.</summary>
    private static void DrawBorder(byte[] buf, int bufW, int bufH,
        int x, int y, int w, int h, int thickness, byte b, byte g, byte r)
    {
        for (int t = 0; t < thickness; t++)
        {
            // Top + bottom edges.
            for (int dx = x - thickness; dx < x + w + thickness; dx++)
            {
                SetPixel(buf, bufW, bufH, dx, y - 1 - t, b, g, r);
                SetPixel(buf, bufW, bufH, dx, y + h + t, b, g, r);
            }
            // Left + right edges.
            for (int dy = y - thickness; dy < y + h + thickness; dy++)
            {
                SetPixel(buf, bufW, bufH, x - 1 - t, dy, b, g, r);
                SetPixel(buf, bufW, bufH, x + w + t, dy, b, g, r);
            }
        }
    }

    private static void SetPixel(byte[] buf, int w, int h, int x, int y, byte b, byte g, byte r)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int idx = (y * w + x) * 4;
        buf[idx + 0] = b; buf[idx + 1] = g; buf[idx + 2] = r; buf[idx + 3] = 255;
    }

    // ---- Task #018: Motion blur (cursor trail) ----

    /// <summary>Draw a fading cursor trail (motion blur) from recent cursor positions.
    /// <paramref name="recentPositions"/> is a list of (position, ageMs) pairs; older positions
    /// get lower opacity. <paramref name="intensity"/> scales the trail length (0 = none, 1 = max).</summary>
    public static void DrawMotionBlur(
        byte[] buf, int width, int height,
 IReadOnlyList<(Vec2 position, double ageMs)> recentPositions,
        double intensity, int cursorRadius, double baseOpacity)
    {
        if (recentPositions.Count < 2 || intensity <= 0) return;
        var maxAge = recentPositions[^1].ageMs;
        if (maxAge <= 0) return;

        // Draw ghost cursors from oldest to newest, with opacity ramping up.
        for (int i = 0; i < recentPositions.Count - 1; i++)
        {
            var (pos, age) = recentPositions[i];
            var ageFrac = age / maxAge; // 0 = oldest, 1 = newest
            var ghostOpacity = baseOpacity * intensity * (0.15 + 0.85 * ageFrac);
            var ghostRadius = (int)(cursorRadius * (0.5 + 0.5 * ageFrac));
            DrawCursor(buf, width, height, (int)pos.X, (int)pos.Y, ghostRadius, ghostOpacity, CursorButtonState.None);
        }
    }

    // ---- Task #019: Keyboard shortcut display (keycap overlay) ----

    /// <summary>A keycap to render: display text + position (normalized 0..1 of output size) +
    /// opacity. The position defaults to bottom-center (the conventional spot for shortcut hints).</summary>
    public readonly record struct KeycapDisplay(
        string Text,
        double NormalizedX = 0.5,
        double NormalizedY = 0.85,
        double Opacity = 0.9,
        int FontSize = 24);

    /// <summary>Draw a keycap-style overlay showing pressed keys. Renders a rounded rectangle
    /// background + the key text. This is the headless-testable render; the actual key text comes
    /// from the KeyboardHook's VkToName mapping. The display position is configurable (ui-prd).</summary>
    public static void DrawKeycap(
        byte[] buf, int width, int height,
        KeycapDisplay keycap)
    {
        if (keycap.Opacity <= 0 || string.IsNullOrEmpty(keycap.Text)) return;

        var cx = (int)(keycap.NormalizedX * width);
        var cy = (int)(keycap.NormalizedY * height);
        // Size the keycap to the text length.
        var capW = SysMath.Max(40, keycap.Text.Length * (keycap.FontSize / 2) + 24);
        var capH = keycap.FontSize + 16;
        var capX = cx - capW / 2;
        var capY = cy - capH / 2;

        var a = SysMath.Clamp(keycap.Opacity, 0, 1);

        // Draw the keycap background (dark rounded rectangle → simplified as a filled rect).
        for (int dy = 0; dy < capH; dy++)
        {
            for (int dx = 0; dx < capW; dx++)
            {
                // Skip corners for a rounded look (radius = 8).
                var cornerDist = 0;
                if (dx < 8 && dy < 8) cornerDist = (8 - dx) * (8 - dx) + (8 - dy) * (8 - dy);
                else if (dx >= capW - 8 && dy < 8) cornerDist = (dx - capW + 8) * (dx - capW + 8) + (8 - dy) * (8 - dy);
                else if (dx < 8 && dy >= capH - 8) cornerDist = (8 - dx) * (8 - dx) + (dy - capH + 8) * (dy - capH + 8);
                else if (dx >= capW - 8 && dy >= capH - 8) cornerDist = (dx - capW + 8) * (dx - capW + 8) + (dy - capH + 8) * (dy - capH + 8);
                if (cornerDist > 64) continue; // outside the rounded corner

                int px = capX + dx, py = capY + dy;
                if (px < 0 || py < 0 || px >= width || py >= height) continue;
                int idx = (py * width + px) * 4;
                // Keycap: semi-opaque dark background (BGRA: 30,30,40).
                Blend(buf, idx, 40, 30, 30, a);
            }
        }

        // Draw the key text as a simple bitmap font (block characters for letters/digits).
        // For v1 we render a bright dot pattern proportional to the text — a real font render
        // needs DirectWrite/GDI; this proves the positioning + visibility contract.
        var textStartX = capX + 12;
        var textY = capY + capH / 2 - 4;
        for (int ci = 0; ci < keycap.Text.Length && ci < 8; ci++)
        {
            var ch = keycap.Text[ci];
            var charX = textStartX + ci * (keycap.FontSize / 2 + 2);
            // Each character: a small bright cluster (simplified glyph).
            for (int gy = 0; gy < 8; gy++)
            {
                for (int gx = 0; gx < 6; gx++)
                {
                    // Hash the char + position to a deterministic pattern (varies per char → looks like text).
                    var seed = ((int)ch * 31 + gx * 7 + gy * 13) & 0xFF;
                    if (seed > 128) // ~50% fill → blocky text appearance
                    {
                        int px = charX + gx, py = textY + gy;
                        if (px < 0 || py < 0 || px >= width || py >= height) continue;
                        int idx = (py * width + px) * 4;
                        Blend(buf, idx, 255, 255, 255, a); // white text
                    }
                }
            }
        }
    }
}
