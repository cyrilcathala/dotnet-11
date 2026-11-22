using MissionControl.Contracts;

namespace ProbeSimulator;

/// <summary>Un POST /telemetry/{probe} long vers le récepteur par vol actif ; réessaie toutes les 2 s.</summary>
public sealed class TelemetryEmitter(
    FlightRegistry registry, IHttpClientFactory clients, TimeProvider time, ILogger<TelemetryEmitter> logger)
    : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var flight in registry.Started.ReadAllAsync(stoppingToken))
            _ = Task.Run(() => StreamAsync(flight, stoppingToken), stoppingToken);
    }

    private async Task StreamAsync(Flight flight, CancellationToken stoppingToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, flight.Stopped);
        var ct = linked.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var content = new TelemetryContent(FlightTelemetry.Generate(flight, time, ct));
                using var response = await clients.CreateClient("telemetryreceiver")
                    .PostAsync($"/telemetry/{Uri.EscapeDataString(flight.Probe)}", content, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Récepteur de télémétrie injoignable pour {Probe}, nouvel essai dans 2 s", flight.Probe);
            }

            try { await Task.Delay(RetryDelay, time, ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
