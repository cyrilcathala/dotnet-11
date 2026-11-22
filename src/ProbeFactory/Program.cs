using System.Text.Json.Serialization;
using ProbeFactory;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<IProbeRegistry, InMemoryProbeRegistry>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCreateProbe();

var app = builder.Build();
app.UseRequestLocalization(o => o
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "fr")
    .AddSupportedUICultures("en", "fr"));
app.MapDefaultEndpoints();

app.MapCreateProbe();

app.MapGet("/probes/{name}", (string name, IProbeRegistry registry) =>
    registry.Find(name) is { } probe ? Results.Ok(probe) : Results.NotFound());

app.Run();

public partial class Program;
