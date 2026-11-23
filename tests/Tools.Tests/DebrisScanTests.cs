using System.Diagnostics;

namespace Tools.Tests;

public class DebrisScanTests
{
    private static string ToolPath(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MissionControl.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("MissionControl.slnx introuvable"), "tools", file);
    }

    [Fact]
    public async Task Scan_stops_at_first_threatening_debris()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var r = await Process.RunAndCaptureTextAsync("dotnet", ["run", "--file", ToolPath("debris-scan.cs")], cancellationToken: cts.Token);
        Assert.Equal(0, r.ExitStatus.ExitCode);
        Assert.Contains("Débris en (0, 2) : trajectoire divergente, sans danger.", r.StandardOutput);
        Assert.Contains("Débris en (2, 1) : trajectoire divergente, sans danger.", r.StandardOutput);
        Assert.Contains("Débris en (4, 10) : conjonction dans 4 min — manœuvre d'évitement.", r.StandardOutput);
        Assert.DoesNotContain("(6, 3)", r.StandardOutput);
    }
}
