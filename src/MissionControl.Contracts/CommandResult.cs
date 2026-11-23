using System.Text.Json.Serialization;

namespace MissionControl.Contracts;

public record Acknowledged(Guid CommandId, DateTimeOffset At);
public record Rejected(Guid CommandId, string Reason);
public record TimedOut(Guid CommandId, TimeSpan After);
public record ProbeLost(Guid CommandId, string ProbeName);

// Sérialisé sans discriminant. Pour le relire, le classifier choisit le cas
// d'après la propriété qui lui est propre : at, reason, after ou probeName.
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
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
