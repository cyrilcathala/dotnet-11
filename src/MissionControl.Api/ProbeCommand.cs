using System.Text.Json.Serialization;
using MissionControl.Contracts;

namespace MissionControl.Api;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Thrust), "thrust")]
[JsonDerivedType(typeof(Rotate), "rotate")]
[JsonDerivedType(typeof(CapturePhoto), "capture-photo")]
[JsonDerivedType(typeof(SelfDestruct), "self-destruct")]
public closed record ProbeCommand(Guid Id, string ProbeName)
{
    // Hiérarchie fermée : le compilateur connaît tous les cas, le switch est exhaustif sans default.
    public static SimulatorCommandRequest ToRequest(ProbeCommand cmd) => cmd switch
    {
        Thrust t => new SimulatorCommandRequest(t.Id, "thrust", t.DeltaV),
        Rotate r => new SimulatorCommandRequest(r.Id, "rotate", r.Degrees),
        CapturePhoto c => new SimulatorCommandRequest(c.Id, "capture-photo", null),
        SelfDestruct s => new SimulatorCommandRequest(s.Id, "self-destruct", null),
    };
}

public record Thrust(Guid Id, string ProbeName, double DeltaV) : ProbeCommand(Id, ProbeName);
public record Rotate(Guid Id, string ProbeName, double Degrees) : ProbeCommand(Id, ProbeName);
public record CapturePhoto(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
public record SelfDestruct(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
