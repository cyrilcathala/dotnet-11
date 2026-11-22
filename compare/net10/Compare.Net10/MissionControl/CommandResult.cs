namespace MissionControl.Api;

// Pas d'union en .NET 10 : une classe de base, dont chaque cas hérite.
public abstract record CommandResult(Guid CommandId);

public sealed record Acknowledged(Guid CommandId, DateTimeOffset At) : CommandResult(CommandId);
public sealed record Rejected(Guid CommandId, string Reason) : CommandResult(CommandId);
public sealed record TimedOut(Guid CommandId, TimeSpan After) : CommandResult(CommandId);
public sealed record ProbeLost(Guid CommandId, string ProbeName) : CommandResult(CommandId);

public static class CommandResultDescriber
{
    public static string Describe(CommandResult r) => r switch
    {
        Acknowledged => "ack",
        Rejected => "rejected",
        TimedOut => "timeout",
        ProbeLost => "lost",
        _ => throw new ArgumentOutOfRangeException(nameof(r), r, "Résultat inconnu"),
    };
}
