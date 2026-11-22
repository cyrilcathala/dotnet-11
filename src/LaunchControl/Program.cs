using System.Net;
using LaunchControl;
using MissionControl.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<IPreflightRunner, PreflightRunner>();
builder.Services.AddSingleton<CountdownSequence>();
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
            var flight = await clients.CreateClient("probesimulator")
                .PostAsJsonAsync("/flights", new StartFlightRequest(request.ProbeName), ct);
            flight.EnsureSuccessStatusCode();
            return Results.Ok(new LaunchReport(request.ProbeName, "GO", result.Report));
        case PreflightVerdict.NoGo:
            return Results.Json(new LaunchReport(request.ProbeName, "NO-GO", result.Report),
                statusCode: StatusCodes.Status409Conflict);
        default:
            return Results.Problem(detail: "Préflight indisponible", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/diagnostics/countdown-stack", async (CountdownSequence countdown, CancellationToken ct) =>
{
    var stack = await countdown.CaptureStackAsync(ct);
    var frames = stack.Split('\n').Count(line => line.StartsWith("   at ", StringComparison.Ordinal));
    return new CountdownStackResponse(frames, stack);
});

app.Run();

public partial class Program;
