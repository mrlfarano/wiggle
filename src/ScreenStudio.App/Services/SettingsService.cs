using System.Text.Json;
using ScreenStudio.Core.Cursor;
using ScreenStudio.Core.Export;
using ScreenStudio.Core.Smoothing;
using ScreenStudio.Core.Zoom;

namespace ScreenStudio.App.Services;

/// <summary>Task #015 — persistence layer. The engine's settings (ExportSettings,
/// CursorCustomization, ZoomDetectionOptions) are in-memory only (ui-prd §9.1 gap). This
/// service loads/saves them as a single JSON file so app preferences persist across runs.
///
/// Stored under %LOCALAPPDATA%/ScreenStudio/settings.json (Windows idiom for per-user app data).</summary>
public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenStudio");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>The aggregated, persistable app settings.</summary>
    public sealed class AppSettings
    {
        public SmoothingIntensity DefaultSmoothing { get; set; } = SmoothingIntensity.Medium;
        public ZoomMode DefaultZoomMode { get; set; } = ZoomMode.Automatic;
        public ZoomDetectionOptions ZoomOptions { get; set; } = new();
        public CursorCustomization Cursor { get; set; } = new();
        public ExportSettings Export { get; set; } = new();
        public string? PreferredMicrophone { get; set; }
        public Dictionary<string, string> Hotkeys { get; set; } = new();
    }

    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        Current = Load();
    }

    /// <summary>Load settings from disk, or defaults if absent/corrupt.</summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (loaded != null) return loaded;
            }
        }
        catch { /* corrupt settings → fall back to defaults */ }
        return new AppSettings();
    }

    /// <summary>Save current settings to disk. Creates the directory if needed.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Current, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* best-effort; settings are non-critical */ }
    }

    /// <summary>Path to the settings file (for diagnostics / the About panel).</summary>
    public static string SettingsFilePath => SettingsPath;
}
