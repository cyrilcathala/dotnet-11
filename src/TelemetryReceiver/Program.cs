using MissionControl.Contracts;
using TelemetryReceiver;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddRequestDecompression();
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<IFlightRecorder, InMemoryFlightRecorder>();

var app = builder.Build();
app.UseRequestDecompression();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference();

app.MapIngest();

app.MapGet("/telemetry/stats", (TelemetryStore store) => store.Stats);

app.MapGet("/telemetry/{probe}/latest", (string probe, TelemetryStore store) =>
    store.Latest(probe) is { } frame ? Results.Ok(frame) : Results.NotFound());

app.Run();

public partial class Program;
