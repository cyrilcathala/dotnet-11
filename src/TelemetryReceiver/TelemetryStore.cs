using MissionControl.Contracts;

namespace TelemetryReceiver;

/// <summary>Dernière trame par sonde et dédoublonnage par PacketId (un ensemble par sonde).</summary>
public sealed class TelemetryStore
{
    private sealed class ProbeState
    {
        public readonly HashSet<TelemetryFrame> Seen = new(PacketIdComparer.Instance);
        public TelemetryFrame? Latest;
    }

    private readonly Dictionary<string, ProbeState> _probes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();
    private long _received;
    private long _duplicates;

    public bool TryAdd(TelemetryFrame frame)
    {
        lock (_gate)
        {
            if (!_probes.TryGetValue(frame.ProbeName, out var state))
                _probes[frame.ProbeName] = state = new ProbeState();

            if (!state.Seen.Add(frame))
            {
                _duplicates++;
                return false;
            }
            state.Latest = frame;
            _received++;
            return true;
        }
    }

    public TelemetryFrame? Latest(string probe)
    {
        lock (_gate)
            return _probes.TryGetValue(probe, out var state) ? state.Latest : null;
    }

    public TelemetryStats Stats
    {
        get
        {
            lock (_gate)
                return new TelemetryStats(_received, _duplicates);
        }
    }
}
