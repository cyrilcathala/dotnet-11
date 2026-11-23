namespace MissionControl.Api;

public record Acknowledged(Guid CommandId, DateTimeOffset At);
public record Rejected(Guid CommandId, string Reason);
public record TimedOut(Guid CommandId, TimeSpan After);
public record ProbeLost(Guid CommandId, string ProbeName);

public union CommandResult(Acknowledged, Rejected, TimedOut, ProbeLost)
{
    // Union : les cas sont connus, le switch est exhaustif sans default.
    public static string Describe(CommandResult r) => r switch
    {
        Acknowledged => "ack",
        Rejected => "rejected",
        TimedOut => "timeout",
        ProbeLost => "lost",
    };
}
