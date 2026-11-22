using System.Collections.Concurrent;
using System.Threading.Channels;
using MissionControl.Contracts;

namespace ProbeSimulator;

/// <summary>Un vol en cours ; <see cref="Stopped"/> est annulé à l'autodestruction.</summary>
public sealed class Flight(string probe)
{
    private readonly CancellationTokenSource _stop = new();

    public string Probe { get; } = probe;
    public CancellationToken Stopped => _stop.Token;
    public bool IsStopped => _stop.IsCancellationRequested;
    public void Stop() => _stop.Cancel();
}

public sealed class FlightRegistry
{
    private readonly ConcurrentDictionary<string, Flight> _flights = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<Flight> _started = Channel.CreateUnbounded<Flight>();

    /// <summary>Vols démarrés, à consommer par l'émetteur de télémétrie.</summary>
    public ChannelReader<Flight> Started => _started.Reader;

    /// <summary>Démarre un vol ; renvoie false si la sonde vole déjà (pas de second flux).</summary>
    public bool Start(string probe)
    {
        var flight = new Flight(probe);
        var current = _flights.AddOrUpdate(probe, flight, (_, existing) => existing.IsStopped ? flight : existing);
        if (!ReferenceEquals(current, flight))
            return false;
        _started.Writer.TryWrite(flight);
        return true;
    }

    /// <summary>Applique une commande ; renvoie null si la sonde n'a jamais volé.</summary>
    public SimulatorCommandOutcome? Apply(string probe, SimulatorCommandRequest cmd)
    {
        if (!_flights.TryGetValue(probe, out var flight))
            return null;
        if (flight.IsStopped)
            return new("lost", "Contact perdu");

        if (cmd.Type == "self-destruct")
        {
            flight.Stop();
            return new("lost", null);
        }
        if (cmd.Type == "thrust" && cmd.Value is not > 0)
            return new("rejected", "Poussée invalide");
        return new("ack", null);
    }
}
