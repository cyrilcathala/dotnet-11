var builder = DistributedApplication.CreateBuilder(args);

var toolsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../tools"));

var probefactory = builder.AddDotnetProject("probefactory", "../ProbeFactory/ProbeFactory.csproj")
    .WithHttpEndpoint();

var telemetryreceiver = builder.AddDotnetProject("telemetryreceiver", "../TelemetryReceiver/TelemetryReceiver.csproj")
    .WithHttpEndpoint();

var probesimulator = builder.AddDotnetProject("probesimulator", "../ProbeSimulator/ProbeSimulator.csproj")
    .WithHttpEndpoint()
    .WithReference(telemetryreceiver);

var launchcontrol = builder.AddDotnetProject("launchcontrol", "../LaunchControl/LaunchControl.csproj")
    .WithHttpEndpoint()
    .WithReference(probefactory)
    .WithReference(probesimulator)
    .WithEnvironment("Preflight__ToolPath", Path.Combine(toolsDirectory, "preflight.cs"));

var missioncontrol = builder.AddDotnetProject("missioncontrol", "../MissionControl.Api/MissionControl.Api.csproj")
    .WithHttpEndpoint()
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
    .WithEnvironment("Console__RepoRoot", Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../..")));

builder.Build().Run();
