using System.Diagnostics;
using System.Text;

namespace LaunchControl;

public enum PreflightVerdict { Go, NoGo, Unavailable }

public sealed record PreflightResult(PreflightVerdict Verdict, string Report);

public interface IPreflightRunner
{
    Task<PreflightResult> RunAsync(string probeName, bool forceStorm, CancellationToken ct);
}

public sealed class PreflightRunner(IConfiguration configuration, IHostEnvironment env) : IPreflightRunner
{
    public async Task<PreflightResult> RunAsync(string probeName, bool forceStorm, CancellationToken ct)
    {
        var toolPath = Path.GetFullPath(configuration["Preflight:ToolPath"] ?? "../../tools/preflight.cs", env.ContentRootPath);

        var psi = new ProcessStartInfo("dotnet",
            ["run", "-e", "LAUNCH_SITE=Kourou", "-e", $"PROBE={probeName}", "-e", $"FORCE_STORM={forceStorm}", "--file", toolPath])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Process API .NET 11 : lance l'outil et capture stdout/stderr en une seule expression.
        var result = await Process.RunAndCaptureTextAsync(psi, ct);

        var report = result.StandardOutput.Trim();
        var verdictLine = report.Split('\n').LastOrDefault()?.Trim();

        var verdict = (result.ExitStatus.ExitCode, verdictLine) switch
        {
            (0, "GO") => PreflightVerdict.Go,
            (1, "NO-GO") => PreflightVerdict.NoGo,
            _ => PreflightVerdict.Unavailable,
        };
        return new PreflightResult(verdict, report);
    }
}
