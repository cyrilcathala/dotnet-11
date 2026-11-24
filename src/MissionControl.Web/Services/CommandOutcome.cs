using System.Text.Json;
using System.Text.Json.Nodes;
using MissionControl.Contracts;

namespace MissionControl.Web.Services;

/// <summary>
/// Le cas de l'union <c>CommandResult</c> tel qu'on l'affiche. Le JSON n'a pas de discriminant :
/// c'est le classifieur structurel déclaré sur l'union (<c>[JsonUnion]</c>) qui retrouve le cas.
/// </summary>
public sealed record CommandOutcome(string Case, string Css, string Detail)
{
    public static CommandOutcome? From(JsonNode? json)
    {
        if (json is not JsonObject) return null;
        try { return From(json.Deserialize<CommandResult>(JsonSerializerOptions.Web)); }
        catch (JsonException) { return null; } // une erreur (problem details) n'est pas un CommandResult
    }

    // Même switch exhaustif que côté Api : un cas ajouté à l'union le fait échouer ici aussi.
    public static CommandOutcome From(CommandResult result) => result switch
    {
        Acknowledged a => new("Acknowledged", "ok", $"acquittée à {a.At:HH:mm:ss.fff} UTC"),
        Rejected r => new("Rejected", "warn", r.Reason),
        TimedOut t => new("TimedOut", "bad", $"après {t.After}"),
        ProbeLost p => new("ProbeLost", "bad", $"contact perdu avec {p.ProbeName}"),
    };
}
