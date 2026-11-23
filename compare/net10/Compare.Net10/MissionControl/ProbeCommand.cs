using System.Text.Json.Serialization;
using MissionControl.Contracts;

namespace MissionControl.Api;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Thrust), "thrust")]
[JsonDerivedType(typeof(Rotate), "rotate")]
[JsonDerivedType(typeof(CapturePhoto), "capture-photo")]
[JsonDerivedType(typeof(SelfDestruct), "self-destruct")]
public abstract record ProbeCommand(Guid Id, string ProbeName)
{
    // Pas de type fermé en .NET 10 : le compilateur ne vérifie pas l'exhaustivité, d'où le default: throw.
    public static SimulatorCommandRequest ToRequest(ProbeCommand cmd) => cmd switch
    {
        Thrust t => new SimulatorCommandRequest(t.Id, "thrust", t.DeltaV),
        Rotate r => new SimulatorCommandRequest(r.Id, "rotate", r.Degrees),
        CapturePhoto c => new SimulatorCommandRequest(c.Id, "capture-photo", null),
        SelfDestruct s => new SimulatorCommandRequest(s.Id, "self-destruct", null),
        _ => throw new ArgumentOutOfRangeException(nameof(cmd), cmd, "Commande inconnue"),
    };
}

public sealed record Thrust(Guid Id, string ProbeName, double DeltaV) : ProbeCommand(Id, ProbeName);
public sealed record Rotate(Guid Id, string ProbeName, double Degrees) : ProbeCommand(Id, ProbeName);
public sealed record CapturePhoto(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
public sealed record SelfDestruct(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
