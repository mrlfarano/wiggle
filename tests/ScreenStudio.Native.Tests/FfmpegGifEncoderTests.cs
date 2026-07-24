using ScreenStudio.Core.Export;
using ScreenStudio.Native.Encoding;

namespace ScreenStudio.Native.Tests;

/// <summary>Task 020 — PRD P2 #4 (Advanced Export). Genuine end-to-end encode of a short clip
/// to GIF via the ffmpeg two-pass palette pipeline (palettegen/paletteuse). Verifies:
///   1. ffmpeg produced a non-empty file,
///   2. the file starts with the "GIF89a" magic (valid animated GIF header),
///   3. ffmpeg itself can probe the stream back as a gif.
///
/// The test is environment-aware: if ffmpeg is not on PATH/known locations it skips cleanly
/// (mirrors the convention in <see cref="FfmpegEncoderTests"/>). FFmpeg 7.1.1 ships the gif
/// encoder/muxer and the palette filters, so this runs as a real encode on the project's
/// reference build host.</summary>
public class FfmpegGifEncoderTests
{
    private static string TempGif() => Path.Combine(Path.GetTempPath(), $"ssc_gif_{Guid.NewGuid():N}.gif");

    private static byte[] GradientFrame(int w, int h, int phase)
    {
        // Same synthetic BGRA generator as FfmpegEncoderTests so GIF behavior is checked
        // against the same kind of real, varying input (not a flat frame).
        var buf = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                buf[i + 0] = (byte)((x + phase) & 0xFF);
                buf[i + 1] = (byte)((y + phase / 2) & 0xFF);
                buf[i + 2] = (byte)((x ^ y) & 0xFF);
                buf[i + 3] = 0xFF;
            }
        return buf;
    }

    [Fact]
    public void Encodes_Real_Gif_With_Valid_Header_And_Decodable_Stream()
    {
        if (!FfmpegEncoder.IsAvailable())
        {
            // Honest skip: ffmpeg not present on this host. We still assert the absence is
            // surfaced correctly, mirroring the H.264 test's contract.
            Assert.Throws<PlatformNotSupportedException>(() =>
                new FfmpegEncoder().Initialize(new ExportSettings { Codec = VideoCodec.GIF }));
            return;
        }

        var path = TempGif();
        try
        {
            // Small resolution keeps the GIF encode fast; 30 frames @ 15fps = 2s of animation.
            const int W = 320, H = 240;
            var settings = new ExportSettings
            {
                Codec = VideoCodec.GIF,
                Resolution = new(W, H),
                FrameRate = 15,
                BitrateKbps = 1_000, // ignored by GIF, but must pass Validate()
                OutputPath = path,
            };

            using var enc = new FfmpegEncoder();
            enc.Initialize(settings);
            for (int f = 0; f < 30; f++)
                enc.WriteFrameRgb32(GradientFrame(W, H, f));
            enc.FinalizeStream();

            // --- Quality validation 1: real file exists and is non-trivially sized ---
            Assert.True(File.Exists(path), "GIF not created");
            var info = new FileInfo(path);
            Assert.True(info.Length > 2_000, $"GIF suspiciously small: {info.Length} bytes");

            // --- Quality validation 2: GIF89a magic header (animated GIF signature) ---
            using var fs = File.OpenRead(path);
            var head = new byte[6];
            var read = fs.Read(head, 0, 6);
            Assert.Equal(6, read);
            var magic = System.Text.Encoding.ASCII.GetString(head, 0, 6);
            Assert.True(magic == "GIF89a" || magic == "GIF87a",
                $"expected GIF magic, got '{magic}' (animated GIFs are GIF89a)");

            // --- Quality validation 3: ffmpeg can probe/decode it back ---
            var probe = ProbeWithFfmpeg(path);
            Assert.Contains("Duration:", probe);
            Assert.Contains("gif", probe, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static string ProbeWithFfmpeg(string path)
    {
        var exe = FfmpegEncoder.FindFfmpeg()!;
        var psi = new System.Diagnostics.ProcessStartInfo(exe, $"-i \"{path}\"")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit(5000);
        return err; // ffmpeg writes stream info to stderr
    }
}
