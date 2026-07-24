using ScreenStudio.Core.Export;

namespace ScreenStudio.Core.Tests.Export;

/// <summary>Task 020 — PRD P2 #4 (Advanced Export). Verifies the new social/advanced presets
/// (TikTok 9:16, YouTube 1080p, GIF 480p loop, WebM 720p) are present, apply cleanly to the
/// ViewModel, and pass <see cref="ExportSettings.Validate"/>. This is the pure-logic counterpart
/// to the ffmpeg end-to-end test in ScreenStudio.Native.Tests.</summary>
public class AdvancedExportPresetsTests
{
    [Fact]
    public void New_Codec_Enum_Values_Exist()
    {
        // The enum is the integration point the encoder switches on.
        Assert.True(Enum.IsDefined(typeof(VideoCodec), "GIF"));
        Assert.True(Enum.IsDefined(typeof(VideoCodec), "WEBM"));
    }

    [Fact]
    public void Named_Social_Presets_Are_Present()
    {
        var names = ExportSettingsViewModel.Presets.Select(p => p.Name).ToArray();
        Assert.Contains("TikTok 9:16 720p", names);
        Assert.Contains("YouTube 1080p", names);
        Assert.Contains("GIF 480p loop", names);
        Assert.Contains("WebM 720p", names);
    }

    [Fact]
    public void All_Presets_Validate_And_Enable_Export()
    {
        // Every preset — including the new GIF/WEBM ones — must round-trip through ApplyPreset
        // and leave the ViewModel in an exportable state (CanExport == true). This guards
        // against a future Validate() change that would silently disable a preset.
        foreach (var p in ExportSettingsViewModel.Presets)
        {
            var vm = new ExportSettingsViewModel { OutputPath = $"out_{p.Codec}.{ExtFor(p.Codec)}" };
            vm.ApplyPreset(p);
            Assert.True(vm.CanExport, $"preset '{p.Name}' should be valid: {vm.ValidationError}");
        }
    }

    [Fact]
    public void TikTok_Preset_Is_Vertical_9x16()
    {
        var p = Find("TikTok 9:16 720p");
        Assert.Equal(VideoCodec.H264, p.Codec);
        // 9:16 vertical: height > width, 720 wide.
        Assert.Equal(720, p.Resolution.Width);
        Assert.Equal(1280, p.Resolution.Height);
        Assert.Equal(30, p.FrameRate);
    }

    [Fact]
    public void YouTube_Preset_Is_1080p_H264()
    {
        var p = Find("YouTube 1080p");
        Assert.Equal(VideoCodec.H264, p.Codec);
        Assert.Equal(1920, p.Resolution.Width);
        Assert.Equal(1080, p.Resolution.Height);
        Assert.Equal(60, p.FrameRate);
    }

    [Fact]
    public void Gif_Preset_Uses_Gif_Codec_And_480p()
    {
        var p = Find("GIF 480p loop");
        Assert.Equal(VideoCodec.GIF, p.Codec);
        Assert.Equal(854, p.Resolution.Width);
        Assert.Equal(480, p.Resolution.Height);
        Assert.Equal(15, p.FrameRate);
    }

    [Fact]
    public void WebM_Preset_Uses_WebM_Codec_And_720p()
    {
        var p = Find("WebM 720p");
        Assert.Equal(VideoCodec.WEBM, p.Codec);
        Assert.Equal(1280, p.Resolution.Width);
        Assert.Equal(720, p.Resolution.Height);
        Assert.Equal(30, p.FrameRate);
    }

    [Theory]
    [InlineData(VideoCodec.GIF,  15, 1_000)]   // GIF: permissive (15fps ok, bitrate ignored)
    [InlineData(VideoCodec.GIF,  30, 50_000)]  // GIF: 30fps + high bitrate still ok
    [InlineData(VideoCodec.WEBM, 30, 1_000)]   // WebM: standard
    [InlineData(VideoCodec.WEBM, 60, 5_000)]   // WebM: 60fps
    public void New_Codecs_Validate_Under_Their_Relaxed_Contract(VideoCodec codec, int fps, int kbps)
    {
        var s = new ExportSettings
        {
            Codec = codec,
            Resolution = Resolution.HD720p,
            FrameRate = fps,
            BitrateKbps = kbps,
            OutputPath = "out",
        };
        var ex = Record.Exception(() => s.Validate());
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(VideoCodec.GIF,  0)]    // fps too low
    [InlineData(VideoCodec.GIF,  61)]   // fps too high
    [InlineData(VideoCodec.WEBM, 45)]   // fps not 30/60
    public void New_Codecs_Still_Reject_Invalid_FrameRate(VideoCodec codec, int fps)
    {
        var s = new ExportSettings { Codec = codec, FrameRate = fps, BitrateKbps = 1_000 };
        Assert.Throws<InvalidOperationException>(() => s.Validate());
    }

    [Fact]
    public void WebM_Rejects_Out_Of_Range_Bitrate()
    {
        var s = new ExportSettings { Codec = VideoCodec.WEBM, FrameRate = 30, BitrateKbps = 10 };
        Assert.Throws<InvalidOperationException>(() => s.Validate());
    }

    [Fact]
    public void H264_Path_Unchanged_Strict_Contract()
    {
        // Regression guard: the original strict 30/60 + 500–200000kbps contract must still
        // apply to H.264/HEVC after the GIF/WEBM relaxation.
        Assert.Throws<InvalidOperationException>(() =>
            new ExportSettings { Codec = VideoCodec.H264, FrameRate = 45 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new ExportSettings { Codec = VideoCodec.H264, FrameRate = 30, BitrateKbps = 10 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new ExportSettings { Codec = VideoCodec.HEVC, FrameRate = 45 }.Validate());
    }

    private static ExportPreset Find(string name) =>
        ExportSettingsViewModel.Presets.First(p => p.Name == name);

    private static string ExtFor(VideoCodec c) => c switch
    {
        VideoCodec.GIF => "gif",
        VideoCodec.WEBM => "webm",
        _ => "mp4",
    };
}
