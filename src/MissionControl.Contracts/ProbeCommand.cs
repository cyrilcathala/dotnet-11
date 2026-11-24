using System.Text.Json.Serialization;

namespace MissionControl.Contracts;

// Les cas sont déduits de la hiérarchie fermée, sans les déclarer : "$type" vaut le nom du record (« Thrust »…).
// Alternative avec génération de sources : [JsonSourceGenerationOptions(InferClosedTypePolymorphism = true)]
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record ProbeCommand(Guid Id, string ProbeName)
{
    // Hiérarchie fermée : le compilateur connaît tous les cas, le switch est exhaustif sans default.
    public static SimulatorCommandRequest ToRequest(ProbeCommand cmd) => cmd switch
    {
        Thrust t => new SimulatorCommandRequest(t.Id, "thrust", t.DeltaV),
        Rotate r => new SimulatorCommandRequest(r.Id, "rotate", r.Degrees),
        CapturePhoto c => new SimulatorCommandRequest(c.Id, "capture-photo", c.Zoom),
        SelfDestruct s => new SimulatorCommandRequest(s.Id, "self-destruct", null),
    };
}

public record Thrust(Guid Id, string ProbeName, double DeltaV) : ProbeCommand(Id, ProbeName);
public record Rotate(Guid Id, string ProbeName, double Degrees) : ProbeCommand(Id, ProbeName);
public record CapturePhoto(Guid Id, string ProbeName, double Zoom) : ProbeCommand(Id, ProbeName);
public record SelfDestruct(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
