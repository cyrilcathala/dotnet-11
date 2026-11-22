using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using MissionControl.Contracts;
using ProbeFactory;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<IProbeRegistry, InMemoryProbeRegistry>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddValidation(o =>
    o.LocalizerProvider = (_, factory) => factory.Create(typeof(ValidationMessages)));

var app = builder.Build();
app.UseRequestLocalization(o => o
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "fr")
    .AddSupportedUICultures("en", "fr"));
app.MapDefaultEndpoints();

app.MapPost("/probes", (CreateProbeRequest request, IProbeRegistry registry) =>
{
    var probe = registry.Add(request.Name, request.Kind);
    return Results.Created($"/probes/{probe.Name}", probe);
});

app.MapGet("/probes/{name}", (string name, IProbeRegistry registry) =>
    registry.Find(name) is { } probe ? Results.Ok(probe) : Results.NotFound());

app.Run();

public partial class Program;
