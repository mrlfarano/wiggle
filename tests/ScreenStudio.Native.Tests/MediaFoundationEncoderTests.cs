using ScreenStudio.Core.Export;
using ScreenStudio.Native.Encoding;

namespace ScreenStudio.Native.Tests;

/// <summary>Verifies the real Media Foundation H.264 encoder. On a desktop Windows with a
/// functional media stack it must produce a valid MP4 (ftyp box). On an N-edition or
/// headless/container Windows where mfplat.dll is a stub, the encoder must cleanly report
/// unavailability rather than crash with a COM cast error.</summary>
public class MediaFoundationEncoderTests
{
    private static string TempMp4()
        => Path.Combine(Path.GetTempPath(), $"ssctest_{Guid.NewGuid():N}.mp4");

    private static byte[] GradientFrame(int width, int height, int phase)
    {
        var buf = new byte[width * height * 4]; // BGRA
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                buf[i + 0] = (byte)((x + phase) & 0xFF);       // B
                buf[i + 1] = (byte)((y + phase / 2) & 0xFF);   // G
                buf[i + 2] = (byte)((x ^ y) & 0xFF);           // R
                buf[i + 3] = 0xFF;                             // A
            }
        return buf;
    }

    [Fact]
    public void Encodes_Frames_To_Valid_Mp4_When_MediaFoundation_Available()
    {
        if (!MediaFoundationEncoder.IsAvailable())
        {
            // Honest skip: MF is non-functional on this host. Assert the encoder reports it.
            var enc = new MediaFoundationEncoder();
            var ex = Assert.Throws<PlatformNotSupportedException>(
                () => enc.Initialize(new ExportSettings { OutputPath = TempMp4() }));
            Assert.Contains("Media Foundation is not functional", ex.Message);
            enc.Dispose();
            return;
        }

        var path = TempMp4();
        try
        {
            var settings = new ExportSettings
            {
                Codec = VideoCodec.H264,
                Resolution = Resolution.HD720p,
                FrameRate = 30,
                BitrateKbps = 4000,
                OutputPath = path,
            };

            using var enc = new MediaFoundationEncoder();
            enc.Initialize(settings);
            for (int f = 0; f < 30; f++)
                enc.WriteFrameRgb32(GradientFrame(settings.Resolution.Width, settings.Resolution.Height, f));
            enc.FinalizeStream();

            // Verification: a real MP4 with an ftyp box.
            Assert.True(File.Exists(path), "output MP4 was not created");
            Assert.True(new FileInfo(path).Length > 1024, $"MP4 suspiciously small");
            using var fs = File.OpenRead(path);
            var head = new byte[12];
            Assert.Equal(12, fs.Read(head, 0, 12));
            Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(head, 4, 4));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
