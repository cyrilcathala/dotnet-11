using MissionControl.Contracts;
using ProbeSimulator;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FlightRegistry>();
builder.Services.AddHostedService<TelemetryEmitter>();
builder.Services.AddHttpClient("telemetryreceiver", c =>
{
    c.BaseAddress = new Uri("https+http://telemetryreceiver");
    c.Timeout = Timeout.InfiniteTimeSpan;
});

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapPost("/flights", (StartFlightRequest request, FlightRegistry flights) =>
{
    flights.Start(request.ProbeName);
    return Results.Accepted();
});

app.MapPost("/flights/{probe}/commands", (string probe, SimulatorCommandRequest command, FlightRegistry flights) =>
    flights.Apply(probe, command) is { } outcome ? Results.Ok(outcome) : Results.NotFound());

app.Run();

public partial class Program;
