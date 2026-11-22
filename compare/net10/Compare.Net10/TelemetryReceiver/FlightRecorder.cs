using System.Collections.Concurrent;

namespace TelemetryReceiver;

/// <summary>Boîte noire : archive les octets bruts reçus pour chaque sonde.</summary>
public interface IFlightRecorder
{
    Task AppendAsync(string probe, Stream raw, CancellationToken ct);
}

public sealed class InMemoryFlightRecorder : IFlightRecorder
{
    private readonly ConcurrentDictionary<string, MemoryStream> _recordings = new();

    public async Task AppendAsync(string probe, Stream raw, CancellationToken ct)
    {
        var recording = _recordings.GetOrAdd(probe, _ => new MemoryStream());
        var buffer = new MemoryStream();
        await raw.CopyToAsync(buffer, ct);
        lock (recording)
            buffer.WriteTo(recording);
    }

    public byte[] Snapshot(string probe)
    {
        if (!_recordings.TryGetValue(probe, out var recording))
            return [];
        lock (recording)
            return recording.ToArray();
    }
}
