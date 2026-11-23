namespace MissionControl.Web.Services;

public sealed record MissionStep(int Number, string Route, string Phase, string Title, string Service, string[] Features);

/// <summary>Une nouveauté .NET 11, pour le récapitulatif de fin de session.</summary>
public sealed record Novelty(string Name, string Summary, int Step, string DocUrl);

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

    const string Runtime = "https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime";
    const string Libraries = "https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries";
    const string Sdk = "https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/sdk";
    const string AspNetCore = "https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-11";
    const string CSharp = "https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15";
    const string Aspire = "https://aspire.dev/whats-new/aspire-17/";

    /// <summary>Le récapitulatif de l'écran d'accueil, dans l'ordre des étapes.</summary>
    public static readonly Novelty[] Novelties =
    [
        new("dotnetup", "Installe et met à jour les SDK .NET en une commande.", 0, Sdk),
        new("Aspire 17 · AddDotnetProject", "Déclare un projet par son chemin, sans type généré.", 0, Aspire),
        new("aspire describe --format mermaid", "Exporte la topologie de l'AppHost en diagramme.", 0, Aspire),
        new("Certificats de dev sous WSL", "dev-certs https --trust approuve WSL et Windows.", 0, AspNetCore),
        new("Validation asynchrone", "Les règles async (unicité…) s'exécutent avant l'endpoint.", 1, AspNetCore),
        new("Messages de validation localisés", "Les messages viennent d'un .resx, sans attribut par champ.", 1, AspNetCore),
        new("Collection expressions with(...)", "Passe un comparateur ou une capacité dans [ ... ].", 1, CSharp),
        new("Process.RunAndCaptureTextAsync", "Lance un processus et récupère sa sortie en un appel.", 2, Libraries),
        new("dotnet run -e", "Passe des variables d'environnement en ligne de commande.", 2, Sdk),
        new("Runtime async", "Le runtime gère l'async : stack traces deux fois plus courtes.", 2, Runtime),
        new("Zstandard", "Compression Zstd intégrée à System.IO.Compression.", 3, Libraries),
        new("CBOR incrémental", "CborReaderOptions : lecture par morceaux.", 3, Libraries),
        new("ReadOnlySequenceStream", "Un Stream sur une ReadOnlySequence, sans copie.", 3, Libraries),
        new("EqualityComparer<T>.Create", "Un comparateur simplifié.", 3, Libraries),
        new("#:include", "Une app fichier unique inclut d'autres fichiers .cs.", 4, Sdk),
        new("StringStream", "Lit une chaîne comme un Stream, sans intermédiaire.", 4, Libraries),
        new("Indexeur d'extension", "extension compatible avec les indexeurs.", 4, CSharp),
        new("break outer", "Sortir d'une boucle nommée, sans goto.", 4, CSharp),
        new("closed record", "Hiérarchie fermée et le switch devient exhaustif.", 5, CSharp),
        new("union", "Un type « l'un ou l'autre », sérialisé sans discriminant.", 5, CSharp),
        new("OpenAPI anyOf", "Les unions sont décrites en anyOf dans le document OpenAPI.", 5, AspNetCore),
        new("Flux JSONL", "SerializeAsyncEnumerable(topLevelValues: true) : un objet par ligne.", 6, Libraries),
        new("OpenTelemetry natif", "ASP.NET Core émet ses traces sans package d'instrumentation.", 6, AspNetCore),
        new("dotnet new mcpserver", "Un template du SDK pour créer un serveur MCP.", 7, Sdk),
        new("Transport HTTP sans état", "Le serveur MCP répond en HTTP, sans session à garder.", 7, Sdk),
    ];

    public static MissionStep? Next(MissionStep step) => Steps.FirstOrDefault(s => s.Number == step.Number + 1);
    public static MissionStep? Previous(MissionStep step) => Steps.FirstOrDefault(s => s.Number == step.Number - 1);
    public static MissionStep ByNumber(int number) => Steps[number];
}
