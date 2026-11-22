using System.ComponentModel.DataAnnotations;
using MissionControl.Contracts;

namespace ProbeFactory;

public sealed record CreateProbeRequest(
    [property: Required, StringLength(32, MinimumLength = 3), UniqueProbeName] string Name,
    ProbeKind Kind);
