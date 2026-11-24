// Ports figés pour que les URLs restent stables d'un lancement à l'autre
// (console : 5194 et mcp : 6277 viennent de leur launchSettings.json).
var builder = DistributedApplication.CreateBuilder(args);

var toolsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../tools"));

var probefactory = builder.AddDotnetProject("probefactory", "../ProbeFactory/ProbeFactory.csproj")
    .WithHttpEndpoint(port: 5101)
    .WithScalar();

var telemetryreceiver = builder.AddDotnetProject("telemetryreceiver", "../TelemetryReceiver/TelemetryReceiver.csproj")
    .WithHttpEndpoint(port: 5102)
    .WithScalar();

var probesimulator = builder.AddDotnetProject("probesimulator", "../ProbeSimulator/ProbeSimulator.csproj")
    .WithHttpEndpoint(port: 5103)
    .WithScalar()
    .WithReference(telemetryreceiver);

var launchcontrol = builder.AddDotnetProject("launchcontrol", "../LaunchControl/LaunchControl.csproj")
    .WithHttpEndpoint(port: 5104)
    .WithScalar()
    .WithReference(probefactory)
    .WithReference(probesimulator)
    .WithEnvironment("Preflight__ToolPath", Path.Combine(toolsDirectory, "preflight.cs"));

var missioncontrol = builder.AddDotnetProject("missioncontrol", "../MissionControl.Api/MissionControl.Api.csproj")
    .WithHttpEndpoint(port: 5105)
    .WithScalar()
    .WithReference(probesimulator);

var mcp = builder.AddDotnetProject("mcp", "../MissionControl.Mcp/MissionControl.Mcp.csproj")
    .WithReference(launchcontrol)
    .WithReference(missioncontrol)
    .WithReference(telemetryreceiver);

builder.AddExecutable("debris-scan", "dotnet", toolsDirectory, "run", "--file", "debris-scan.cs");

// Console de démonstration (Blazor) : une page par étape, appelle chaque service et lit les sources du dépôt.
builder.AddDotnetProject("console", "../MissionControl.Web/MissionControl.Web.csproj")
    .WithHttpEndpoint()
    .WithExternalHttpEndpoints()
    .WithReference(probefactory)
    .WithReference(launchcontrol)
    .WithReference(probesimulator)
    .WithReference(telemetryreceiver)
    .WithReference(missioncontrol)
    .WithReference(mcp)
    // Adresse du dashboard : la page Incident ouvre directement la trace de la commande.
    .WithEnvironment("Console__DashboardUrl", builder.Configuration["ASPNETCORE_URLS"]?.Split(';')[0] ?? "")
    .WithEnvironment("Console__RepoRoot", Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../..")));

builder.Build().Run();

static class AppHostExtensions
{
    // Ajoute un lien direct vers la référence Scalar de l'API sur le dashboard Aspire.
    public static IResourceBuilder<T> WithScalar<T>(this IResourceBuilder<T> builder)
        where T : IResourceWithEndpoints =>
        builder.WithUrlForEndpoint("http", _ => new() { Url = "/scalar", DisplayText = "Scalar" });
}
