using MissionControl.Web.Components;
using MissionControl.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Un client nommé par service de la mission : les adresses viennent de la découverte de services d'Aspire.
foreach (var service in MissionApi.Services)
    builder.Services.AddHttpClient(service, c => c.BaseAddress = new Uri($"https+http://{service}"));

builder.Services.AddSingleton<MissionApi>();
builder.Services.AddSingleton<MissionState>();
builder.Services.AddSingleton<SourceFiles>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapDefaultEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
