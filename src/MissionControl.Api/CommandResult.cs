namespace MissionControl.Api;

public record Acknowledged(Guid CommandId, DateTimeOffset At);
public record Rejected(Guid CommandId, string Reason);
public record TimedOut(Guid CommandId, TimeSpan After);
public record ProbeLost(Guid CommandId, string ProbeName);

public union CommandResult(Acknowledged, Rejected, TimedOut, ProbeLost);
