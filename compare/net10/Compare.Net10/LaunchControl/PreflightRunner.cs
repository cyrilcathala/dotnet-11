using System.Diagnostics;

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

        // .NET 10 : ProcessStartInfo, redirections, variables d'environnement et lecture des flux à la main.
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--file");
        psi.ArgumentList.Add(toolPath);
        psi.Environment["LAUNCH_SITE"] = "Kourou";
        psi.Environment["PROBE"] = probeName;
        psi.Environment["FORCE_STORM"] = forceStorm.ToString();

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct); // à vider aussi, sinon blocage possible
        await process.WaitForExitAsync(ct);

        var report = (await stdout).Trim();
        _ = await stderr;
        var verdictLine = report.Split('\n').LastOrDefault()?.Trim();

        var verdict = (process.ExitCode, verdictLine) switch
        {
            (0, "GO") => PreflightVerdict.Go,
            (1, "NO-GO") => PreflightVerdict.NoGo,
            _ => PreflightVerdict.Unavailable,
        };
        return new PreflightResult(verdict, report);
    }
}
