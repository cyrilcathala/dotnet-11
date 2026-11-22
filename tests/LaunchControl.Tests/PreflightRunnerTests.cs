using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LaunchControl.Tests;

public sealed class PreflightRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MissionControl.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("MissionControl.slnx introuvable");
    }

    private static PreflightRunner Runner(string toolPath) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Preflight:ToolPath"] = toolPath }).Build(), new FakeEnv());

    private static string RealTool => Path.Combine(RepoRoot(), "tools", "preflight.cs");

    [Fact]
    public async Task Clear_weather_is_go()
    {
        using var cts = new CancellationTokenSource(Timeout);
        var r = await Runner(RealTool).RunAsync("Pioneer-11", false, cts.Token);
        Assert.Equal(PreflightVerdict.Go, r.Verdict);
        Assert.Contains("Orages : aucun", r.Report);
    }

    [Fact]
    public async Task Forced_storm_is_no_go()
    {
        using var cts = new CancellationTokenSource(Timeout);
        var r = await Runner(RealTool).RunAsync("Pioneer-11", true, cts.Token);
        Assert.Equal(PreflightVerdict.NoGo, r.Verdict);
        Assert.Contains("cellule orageuse", r.Report);
    }

    [Fact]
    public async Task Broken_tool_is_unavailable()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var broken = Path.Combine(dir.FullName, "broken.cs");
            await File.WriteAllTextAsync(broken, "this is not valid C#;", TestContext.Current.CancellationToken);
            using var cts = new CancellationTokenSource(Timeout);
            var r = await Runner(broken).RunAsync("Pioneer-11", false, cts.Token);
            Assert.Equal(PreflightVerdict.Unavailable, r.Verdict);
        }
        finally { dir.Delete(true); }
    }
}

internal sealed class FakeEnv : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "LaunchControl";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
