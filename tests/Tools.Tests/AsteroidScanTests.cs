using System.Diagnostics;

namespace Tools.Tests;

public class AsteroidScanTests
{
    private static string ToolPath(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MissionControl.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("MissionControl.slnx introuvable"), "tools", file);
    }

    [Fact]
    public async Task Scan_reports_first_asteroid()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var r = await Process.RunAndCaptureTextAsync("dotnet", ["run", "--file", ToolPath("asteroid-scan.cs")], cancellationToken: cts.Token);
        Assert.Equal(0, r.ExitStatus.ExitCode);
        Assert.Contains("Astéroïde détecté en (0, 10) — trajectoire corrigée.", r.StandardOutput);
    }
}
