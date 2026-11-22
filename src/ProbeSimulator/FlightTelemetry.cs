using System.Runtime.CompilerServices;
using MissionControl.Contracts;

namespace ProbeSimulator;

public static class FlightTelemetry
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Une trame toutes les 500 ms ; toutes les 5 trames, la précédente est renvoyée (même PacketId).
    /// Les ids viennent du compteur de la sonde : ils continuent après un nouvel essai ou un relancement.
    /// </summary>
    public static async IAsyncEnumerable<TelemetryFrame> Generate(
        Flight flight, TimeProvider time, [EnumeratorCancellation] CancellationToken ct)
    {
        while (true)
        {
            var id = flight.NextPacketId();
            var frame = new TelemetryFrame(id, flight.Probe, time.GetUtcNow(),
                AltitudeKm: 410 + 0.1 * id,
                VelocityKmS: 7.66 + 0.001 * id,
                BatteryPercent: Math.Max(0, 100 - 0.05 * id));
            yield return frame;
            if (id % 5 == 0)
                yield return frame;
            await Task.Delay(Interval, time, ct);
        }
    }
}
