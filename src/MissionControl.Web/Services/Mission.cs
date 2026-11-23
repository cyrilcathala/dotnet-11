namespace MissionControl.Web.Services;

public sealed record MissionStep(int Number, string Route, string Phase, string Title, string Service, string[] Features);

/// <summary>Le déroulé de la démo, dans l'ordre du runbook (docs/demo-runbook.md).</summary>
public static class Mission
{
    public static readonly MissionStep[] Steps =
    [
        new(0, "preparation", "Préparation", "Centre de contrôle", "AppHost",
            ["dotnetup", "Aspire 17 · AddDotnetProject", "aspire describe --format mermaid", "Certificats WSL"]),
        new(1, "fabrication", "Fabrication", "Construire la sonde", "ProbeFactory",
            ["Validation asynchrone", "Messages localisés", "Collection expressions with(...)"]),
        new(2, "lancement", "Lancement", "Décollage", "LaunchControl",
            ["Process.RunAndCaptureTextAsync", "dotnet run -e", "Runtime async"]),
        new(3, "vol", "En vol", "Télémétrie", "ProbeSimulator → TelemetryReceiver",
            ["Zstandard", "CBOR incrémental", "ReadOnlySequenceStream", "EqualityComparer<T>.Create"]),
        new(4, "debris", "Interlude", "Débris en orbite", "debris-scan.cs",
            ["#:include", "StringStream", "Indexeur d'extension", "break outer"]),
        new(5, "pilotage", "Pilotage", "Commander la sonde", "MissionControl",
            ["closed record", "union", "OpenAPI anyOf"]),
        new(6, "incident", "Incident", "Perte de contact", "MissionControl",
            ["ProbeLost", "Journal JSONL", "OpenTelemetry natif"]),
        new(7, "mcp", "Final", "Un agent aux commandes", "MissionControl.Mcp",
            ["dotnet new mcpserver", "Transport HTTP sans état"]),
    ];

    public static MissionStep? Next(MissionStep step) => Steps.FirstOrDefault(s => s.Number == step.Number + 1);
    public static MissionStep? Previous(MissionStep step) => Steps.FirstOrDefault(s => s.Number == step.Number - 1);
    public static MissionStep ByNumber(int number) => Steps[number];
}
