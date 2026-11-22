namespace MissionControl.Contracts;

public enum ProbeKind { Orbiter, Lander, Rover }

public sealed record ProbeDto(string Name, ProbeKind Kind, DateTimeOffset CreatedAt);

public sealed record LaunchRequest(string ProbeName);

/// <param name="Status">"GO" ou "NO-GO".</param>
public sealed record LaunchReport(string ProbeName, string Status, string Weather);

public sealed record StartFlightRequest(string ProbeName);

/// <param name="Type">"thrust", "rotate", "capture-photo" ou "self-destruct".</param>
public sealed record SimulatorCommandRequest(Guid CommandId, string Type, double? Value);

/// <param name="Outcome">"ack", "rejected" ou "lost".</param>
public sealed record SimulatorCommandOutcome(string Outcome, string? Reason);

public sealed record TelemetryFrame(
    long PacketId, string ProbeName, DateTimeOffset Timestamp,
    double AltitudeKm, double VelocityKmS, double BatteryPercent);

public sealed record TelemetryStats(long Received, long Duplicates);
