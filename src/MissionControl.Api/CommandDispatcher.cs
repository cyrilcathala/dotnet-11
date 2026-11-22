using System.Net;
using Microsoft.Extensions.Options;
using MissionControl.Contracts;

namespace MissionControl.Api;

public sealed class CommandDispatcherOptions
{
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed class CommandDispatcher(IHttpClientFactory clients, IOptions<CommandDispatcherOptions> options, TimeProvider time)
{
    public async Task<CommandResult> DispatchAsync(ProbeCommand cmd, CancellationToken ct)
    {
        var timeout = options.Value.CommandTimeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        SimulatorCommandOutcome? outcome;
        try
        {
            var response = await clients.CreateClient("probesimulator").PostAsJsonAsync(
                $"/flights/{Uri.EscapeDataString(cmd.ProbeName)}/commands", ToRequest(cmd), cts.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new Rejected(cmd.Id, "Sonde inconnue ou au sol");
            response.EnsureSuccessStatusCode();
            outcome = await response.Content.ReadFromJsonAsync<SimulatorCommandOutcome>(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new TimedOut(cmd.Id, timeout);
        }

        return outcome?.Outcome switch
        {
            "ack" => new Acknowledged(cmd.Id, time.GetUtcNow()),
            "lost" => new ProbeLost(cmd.Id, cmd.ProbeName),
            _ => new Rejected(cmd.Id, outcome?.Reason ?? "Rejetée par la sonde"),
        };
    }

    public static SimulatorCommandRequest ToRequest(ProbeCommand cmd) => cmd switch
    {
        Thrust t => new SimulatorCommandRequest(t.Id, "thrust", t.DeltaV),
        Rotate r => new SimulatorCommandRequest(r.Id, "rotate", r.Degrees),
        CapturePhoto c => new SimulatorCommandRequest(c.Id, "capture-photo", null),
        SelfDestruct s => new SimulatorCommandRequest(s.Id, "self-destruct", null),
    };

    public static string Describe(CommandResult r) => r switch
    {
        Acknowledged => "ack",
        Rejected => "rejected",
        TimedOut => "timeout",
        ProbeLost => "lost",
    };
}
