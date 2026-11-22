using System.Collections.Concurrent;
using MissionControl.Contracts;

namespace ProbeFactory;

public interface IProbeRegistry
{
    ValueTask<bool> IsTakenAsync(string name, CancellationToken ct);
    ProbeDto Add(string name, ProbeKind kind);
    ProbeDto? Find(string name);
}

public sealed class InMemoryProbeRegistry : IProbeRegistry
{
    // C# 15, arguments d'expression de collection : le comparateur est passé au constructeur du HashSet.
    private readonly HashSet<string> _reserved = [with(StringComparer.OrdinalIgnoreCase), "Voyager", "Apollo"];
    private readonly ConcurrentDictionary<string, ProbeDto> _probes = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<bool> IsTakenAsync(string name, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct); // registre distant simulé
        return _reserved.Contains(name) || _probes.ContainsKey(name);
    }

    public ProbeDto Add(string name, ProbeKind kind)
    {
        var probe = new ProbeDto(name, kind, DateTimeOffset.UtcNow);
        return _probes.TryAdd(name, probe) ? probe : _probes[name];
    }

    public ProbeDto? Find(string name) => _probes.GetValueOrDefault(name);
}
