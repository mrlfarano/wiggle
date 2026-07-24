using System.Diagnostics;
using System.Text.RegularExpressions;
using ScreenStudio.Native.Encoding;

namespace ScreenStudio.E2E.Tests.Validators;

/// <summary>Step 3 — artifact validators. Assert the produced MP4 is a valid container (ftyp
/// box) + decode-probe via ffmpeg to extract duration + frame count. If ffmpeg is absent,
/// tests that touch these throw a Skip (honest skip, not an opaque failure).</summary>
public static class ArtifactValidator
{
    /// <summary>Assert the file exists, is non-trivially sized, and starts with an ftyp box.
    /// Returns the ffmpeg probe output (stderr) for further assertions.</summary>
    public static string AssertValidMp4(string path)
    {
        Assert.True(File.Exists(path), $"MP4 not created at {path}");
        var info = new FileInfo(path);
        Assert.True(info.Length > 1000, $"MP4 suspiciously small: {info.Length} bytes");

        // ftyp box: bytes 4-7 are ASCII "ftyp".
        using var fs = File.OpenRead(path);
        var head = new byte[12];
        var read = fs.Read(head, 0, 12);
        Assert.True(read >= 8, "file too short to contain a box header");
        var boxType = System.Text.Encoding.ASCII.GetString(head, 4, 4);
        Assert.True(boxType == "ftyp", $"expected ftyp box, got '{boxType}'");

        return ProbeWithFfmpeg(path);
    }

    /// <summary>Parse "Duration: 00:00:02.00" from ffmpeg probe output → seconds.</summary>
    public static double ProbeDurationSeconds(string ffmpegProbeOutput)
    {
        var m = Regex.Match(ffmpegProbeOutput, @"Duration:\s+(\d+):(\d+):(\d+(?:\.\d+)?)");
        Assert.True(m.Success, $"no Duration in probe output:\n{ffmpegProbeOutput}");
        var h = int.Parse(m.Groups[1].Value);
        var min = int.Parse(m.Groups[2].Value);
        var sec = double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        return h * 3600 + min * 60 + sec;
    }

    /// <summary>Count actual decoded frames via ffmpeg null-muxer. Catches truncated exports.</summary>
    public static int CountDecodedFrames(string path)
    {
        var probe = RunFfmpeg($"-i \"{path}\" -map 0:v:0 -f null -", expectExit: false);
        var m = Regex.Match(probe, @"frame=\s*(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }

    public static string ProbeWithFfmpeg(string path) => RunFfmpeg($"-i \"{path}\"", expectExit: false);

    private static string RunFfmpeg(string args, bool expectExit = true)
    {
        var exe = FfmpegEncoder.FindFfmpeg()
            ?? throw new SkipException("ffmpeg not found — skipping artifact-validation assertions.");
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new SkipException("failed to start ffmpeg");
        var err = p.StandardError.ReadToEnd();
        // ffmpeg writes stream info to stderr and exits non-zero when only probing (-i with no output)
        p.WaitForExit(10000);
        return err;
    }
}

/// <summary>xUnit has no built-in SkipException; this throws to mark a test as skipped
/// (caught by the test runner as inconclusive-ish). Used when ffmpeg is absent.</summary>
public sealed class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}
