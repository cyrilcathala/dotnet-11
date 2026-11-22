extern alias classicasync;
using System.Net;
using LaunchControl;
using MissionControl.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<IPreflightRunner, PreflightRunner>();
builder.Services.AddSingleton<CountdownSequence>();
// La même classe, compilée sans runtime async (projet LaunchControl.ClassicAsync).
builder.Services.AddSingleton<classicasync::LaunchControl.CountdownSequence>();
builder.Services.AddHttpClient("probefactory", c => c.BaseAddress = new Uri("https+http://probefactory"));
builder.Services.AddHttpClient("probesimulator", c => c.BaseAddress = new Uri("https+http://probesimulator"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapPost("/launches", async (LaunchRequest request, IHttpClientFactory clients, IPreflightRunner preflight,
    CancellationToken ct, bool forceStorm = false) =>
{
    var probeResponse = await clients.CreateClient("probefactory")
        .GetAsync($"/probes/{Uri.EscapeDataString(request.ProbeName)}", ct);
    if (probeResponse.StatusCode == HttpStatusCode.NotFound)
        return Results.NotFound();
    probeResponse.EnsureSuccessStatusCode();

    var result = await preflight.RunAsync(request.ProbeName, forceStorm, ct);

    switch (result.Verdict)
    {
        case PreflightVerdict.Go:
            HttpResponseMessage? flight = null;
            try
            {
                flight = await clients.CreateClient("probesimulator")
                    .PostAsJsonAsync("/flights", new StartFlightRequest(request.ProbeName), ct);
            }
            catch (HttpRequestException) { } // simulateur injoignable
            if (flight is not { IsSuccessStatusCode: true })
                return Results.Problem(detail: "Simulateur indisponible", statusCode: StatusCodes.Status503ServiceUnavailable);
            return Results.Ok(new LaunchReport(request.ProbeName, "GO", result.Report));
        case PreflightVerdict.NoGo:
            return Results.Json(new LaunchReport(request.ProbeName, "NO-GO", result.Report),
                statusCode: StatusCodes.Status409Conflict);
        default:
            return Results.Problem(detail: "Préflight indisponible", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// runtimeAsync=true (défaut) : le code de cette assembly ; false : le même fichier compilé sans runtime async.
app.MapGet("/diagnostics/countdown-stack", async (CountdownSequence withRuntimeAsync,
    classicasync::LaunchControl.CountdownSequence withoutRuntimeAsync, CancellationToken ct, bool runtimeAsync = true) =>
{
    var stack = runtimeAsync
        ? await withRuntimeAsync.CaptureStackAsync(ct)
        : await withoutRuntimeAsync.CaptureStackAsync(ct);
    var frames = stack.Split('\n').Count(line => line.StartsWith("   at ", StringComparison.Ordinal));
    return new CountdownStackResponse(frames, stack, runtimeAsync);
});

app.Run();

public partial class Program;
