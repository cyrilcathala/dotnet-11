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

builder.AddDotnetProject("mcp", "../MissionControl.Mcp/MissionControl.Mcp.csproj")
    .WithReference(launchcontrol)
    .WithReference(missioncontrol)
    .WithReference(telemetryreceiver);

builder.AddExecutable("asteroid-scan", "dotnet", toolsDirectory, "run", "--file", "asteroid-scan.cs");

builder.Build().Run();
