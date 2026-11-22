using MissionControl.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddHttpClient("launchcontrol", c => c.BaseAddress = new Uri("https+http://launchcontrol"));
builder.Services.AddHttpClient("missioncontrol", c => c.BaseAddress = new Uri("https+http://missioncontrol"));
builder.Services.AddHttpClient("telemetryreceiver", c => c.BaseAddress = new Uri("https+http://telemetryreceiver"));

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<MissionTools>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMcp();

app.Run();
