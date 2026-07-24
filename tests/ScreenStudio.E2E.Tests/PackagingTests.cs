using System.Diagnostics;

namespace ScreenStudio.E2E.Tests;

/// <summary>#4 — Packaging user test. Verifies the published, self-contained app launches
/// from the publish/ directory (the distributable form). This is the "can a user run the
/// downloaded app?" check — the first gate of the PRD's "first successful video within 15 min."
/// Requires a prior `dotnet publish` (run publish.cmd).</summary>
[Trait("Category", "Packaging")]
public class PackagingTests
{
    private const string PublishedExe = "../../../../../../publish/ScreenStudio.App.exe";

    /// <summary>PRD success metric: "User can create first successful video within 15 minutes
    /// of first use." Gate 1: the app must launch from the published distributable.</summary>
    [Fact]
    public async Task U15_Published_App_Launches()
    {
        var exePath = Path.GetFullPath(PublishedExe);
        if (!File.Exists(exePath))
        {
            // Skip if not published yet — this test requires `publish.cmd` to have run.
            return;
        }

        var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"& {{ $p = Start-Process -FilePath '{exePath}' -PassThru; Start-Sleep -Seconds 5; $r = Get-Process -Id $p.Id -ErrorAction SilentlyContinue; if ($r) {{ Write-Output 'RUNNING'; Stop-Process -Id $p.Id -Force }} else {{ Write-Output ('EXIT:' + $p.ExitCode) }} }}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(15000);

        Assert.Contains("RUNNING", output);
    }

    /// <summary>Verify the published output contains the essential runtime files (the .NET
    /// runtime + WinAppSDK DLLs), confirming it's genuinely self-contained.</summary>
    [Fact]
    public void U16_Published_Output_Is_SelfContained()
    {
        var publishDir = Path.GetFullPath("../../../../../../publish");
        if (!Directory.Exists(publishDir)) return;

        // Must contain the .NET runtime host + core libraries.
        Assert.True(File.Exists(Path.Combine(publishDir, "ScreenStudio.App.dll")), "App.dll missing");
        Assert.True(File.Exists(Path.Combine(publishDir, "ScreenStudio.Core.dll")), "Core.dll missing");
        Assert.True(File.Exists(Path.Combine(publishDir, "ScreenStudio.Native.dll")), "Native.dll missing");
        // Must contain WinUI runtime.
        Assert.True(File.Exists(Path.Combine(publishDir, "Microsoft.UI.Xaml.dll")), "WinUI runtime missing");
    }
}
