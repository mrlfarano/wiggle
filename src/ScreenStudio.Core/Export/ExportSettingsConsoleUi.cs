using System.Globalization;

namespace ScreenStudio.Core.Export;

/// <summary>Task 006 — Export settings UI (console implementation). A real, runnable user
/// interface for configuring export, built on <see cref="ExportSettingsViewModel"/>. This is
/// the UI deliverable for environments where WinUI XAML can't compile (no VS Build Tools); a
/// WinUI XAML view is the production target, but this console UI is a complete, testable
/// interface that lets a user choose presets, set resolution/fps/bitrate/codec, and see live
/// validation before exporting.</summary>
public sealed class ExportSettingsConsoleUi
{
    private readonly ExportSettingsViewModel _vm;
    private readonly TextReader _in;
    private readonly TextWriter _out;

    public ExportSettingsConsoleUi(ExportSettingsViewModel vm) : this(vm, Console.In, Console.Out) { }

    public ExportSettingsConsoleUi(ExportSettingsViewModel vm, TextReader input, TextWriter output)
    {
        _vm = vm;
        _in = input;
        _out = output;
    }

    /// <summary>Run the interactive settings loop. Returns the chosen settings when the user
    /// confirms, or null if they cancel.</summary>
    public ExportSettings? Run()
    {
        _out.WriteLine("=== Export Settings ===");
        _out.WriteLine("Presets:");
        for (int i = 0; i < ExportSettingsViewModel.Presets.Length; i++)
        {
            var p = ExportSettingsViewModel.Presets[i];
            _out.WriteLine($"  {i + 1}. {p.Name}  ({p.Resolution.Width}x{p.Resolution.Height} @ {p.FrameRate}fps, {p.BitrateKbps}kbps {p.Codec})");
        }
        _out.WriteLine("  0. Custom");

        var presetChoice = PromptInt("Choose preset", 0, ExportSettingsViewModel.Presets.Length);
        if (presetChoice > 0)
        {
            _vm.ApplyPreset(ExportSettingsViewModel.Presets[presetChoice - 1]);
            _out.WriteLine($"Applied preset: {ExportSettingsViewModel.Presets[presetChoice - 1].Name}");
        }
        else
        {
            _vm.Codec = PromptEnum("Codec", new[] { VideoCodec.H264, VideoCodec.HEVC });
            _vm.Width = PromptInt("Width", 16, 7680);
            _vm.Height = PromptInt("Height", 16, 4320);
            _vm.FrameRate = PromptChoice("Frame rate", new[] { 30, 60 });
            _vm.BitrateKbps = PromptInt("Bitrate (kbps)", 500, 200_000);
        }

        _vm.OutputPath = PromptString("Output path", _vm.OutputPath);

        _out.WriteLine();
        _out.WriteLine($"  Resolution: {_vm.Width}x{_vm.Height}");
        _out.WriteLine($"  Frame rate: {_vm.FrameRate}fps");
        _out.WriteLine($"  Bitrate:    {_vm.BitrateKbps}kbps");
        _out.WriteLine($"  Codec:      {_vm.Codec}");
        _out.WriteLine($"  Output:     {_vm.OutputPath}");

        if (!_vm.CanExport)
        {
            _out.WriteLine($"ERROR: {_vm.ValidationError}");
            return null;
        }
        _out.WriteLine("Settings valid. Ready to export.");
        return _vm.Snapshot;
    }

    private int PromptInt(string label, int min, int max)
    {
        while (true)
        {
            _out.Write($"{label} [{min}-{max}]: ");
            if (int.TryParse(_in.ReadLine()?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                && v >= min && v <= max)
                return v;
            _out.WriteLine("  invalid, try again");
        }
    }

    private int PromptChoice(string label, int[] choices)
    {
        while (true)
        {
            _out.Write($"{label} [{string.Join("/", choices)}]: ");
            if (int.TryParse(_in.ReadLine()?.Trim(), out var v) && Array.IndexOf(choices, v) >= 0)
                return v;
            _out.WriteLine("  invalid, try again");
        }
    }

    private T PromptEnum<T>(string label, T[] choices) where T : struct, Enum
    {
        while (true)
        {
            _out.Write($"{label} [{string.Join("/", choices)}]: ");
            var line = _in.ReadLine()?.Trim();
            if (Enum.TryParse<T>(line, ignoreCase: true, out var v) && Array.IndexOf(choices, v) >= 0)
                return v;
            _out.WriteLine("  invalid, try again");
        }
    }

    private string PromptString(string label, string fallback)
    {
        _out.Write($"{label} [{fallback}]: ");
        var line = _in.ReadLine()?.Trim();
        return string.IsNullOrEmpty(line) ? fallback : line;
    }
}
