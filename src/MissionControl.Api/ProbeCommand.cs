using System.Text.Json.Serialization;

namespace MissionControl.Api;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Thrust), "thrust")]
[JsonDerivedType(typeof(Rotate), "rotate")]
[JsonDerivedType(typeof(CapturePhoto), "capture-photo")]
[JsonDerivedType(typeof(SelfDestruct), "self-destruct")]
public closed record ProbeCommand(Guid Id, string ProbeName);

public record Thrust(Guid Id, string ProbeName, double DeltaV) : ProbeCommand(Id, ProbeName);
public record Rotate(Guid Id, string ProbeName, double Degrees) : ProbeCommand(Id, ProbeName);
public record CapturePhoto(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
public record SelfDestruct(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
