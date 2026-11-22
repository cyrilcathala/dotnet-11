using System.Text.Json.Nodes;

namespace MissionControl.Web.Services;

/// <summary>
/// Le cas de l'union <c>CommandResult</c> reconnu à la forme du JSON : l'union sérialise le cas directement,
/// sans discriminant (Acknowledged → at, Rejected → reason, TimedOut → after, ProbeLost → probeName).
/// </summary>
public sealed record CommandOutcome(string Case, string Css, string Detail)
{
    public static CommandOutcome? From(JsonNode? json) => json switch
    {
        JsonObject o when o.ContainsKey("reason") => new("Rejected", "warn", o["reason"]?.ToString() ?? ""),
        JsonObject o when o.ContainsKey("after") => new("TimedOut", "bad", $"après {o["after"]}"),
        JsonObject o when o.ContainsKey("probeName") => new("ProbeLost", "bad", $"contact perdu avec {o["probeName"]}"),
        JsonObject o when o.ContainsKey("at") => new("Acknowledged", "ok", $"acquittée à {o["at"]?.GetValue<DateTimeOffset>():HH:mm:ss.fff} UTC"),
        _ => null,
    };
}

public static class Commands
{
    public static object Build(string type, string probe, double? value)
    {
        var command = new Dictionary<string, object?> { ["type"] = type, ["id"] = Guid.NewGuid(), ["probeName"] = probe };
        if (type == "thrust") command["deltaV"] = value;
        if (type == "rotate") command["degrees"] = value;
        return command;
    }
}
