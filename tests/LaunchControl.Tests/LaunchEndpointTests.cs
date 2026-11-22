using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MissionControl.Contracts;

namespace LaunchControl.Tests;

public sealed class LaunchEndpointTests
{
    private sealed class FakePreflight(PreflightVerdict verdict) : IPreflightRunner
    {
        public List<bool> ForceStormCalls { get; } = [];

        public Task<PreflightResult> RunAsync(string probeName, bool forceStorm, CancellationToken ct) =>
            Task.FromResult(Record(forceStorm, verdict));

        private PreflightResult Record(bool forceStorm, PreflightVerdict v)
        {
            ForceStormCalls.Add(forceStorm);
            return new PreflightResult(v, "rapport");
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private sealed class FakeSimulator
    {
        public List<string> StartedFlights { get; } = [];
    }

    private static (HttpClient Client, FakeSimulator Simulator, FakePreflight Preflight) Create(PreflightVerdict verdict, bool probeExists = true)
    {
        var simulator = new FakeSimulator();
        var fakePreflight = new FakePreflight(verdict);
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IPreflightRunner>(fakePreflight);
            s.AddHttpClient("probefactory").ConfigurePrimaryHttpMessageHandler(() => new FakeHandler(_ =>
                probeExists
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { Name = "Pioneer-11", Kind = "Orbiter", CreatedAt = DateTimeOffset.UtcNow }),
                    }
                    : new HttpResponseMessage(HttpStatusCode.NotFound)));
            s.AddHttpClient("probesimulator").ConfigurePrimaryHttpMessageHandler(() => new FakeHandler(req =>
            {
                var body = req.Content!.ReadFromJsonAsync<StartFlightRequest>().GetAwaiter().GetResult()!;
                simulator.StartedFlights.Add(body.ProbeName);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }));
        }));
        return (factory.CreateClient(), simulator, fakePreflight);
    }

    [Fact]
    public async Task Go_starts_the_flight_and_returns_200()
    {
        var (client, sim, _) = Create(PreflightVerdict.Go);
        var response = await client.PostAsJsonAsync("/launches", new LaunchRequest("Pioneer-11"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<LaunchReport>(TestContext.Current.CancellationToken);
        Assert.Equal(new LaunchReport("Pioneer-11", "GO", "rapport"), report);
        Assert.Single(sim.StartedFlights, "Pioneer-11");
    }

    [Fact]
    public async Task No_go_returns_409_and_starts_nothing()
    {
        var (client, sim, _) = Create(PreflightVerdict.NoGo);
        var response = await client.PostAsJsonAsync("/launches", new LaunchRequest("Pioneer-11"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<LaunchReport>(TestContext.Current.CancellationToken);
        Assert.Equal("NO-GO", report!.Status);
        Assert.Empty(sim.StartedFlights);
    }

    [Fact]
    public async Task Unavailable_preflight_returns_503_never_go()
    {
        var (client, sim, _) = Create(PreflightVerdict.Unavailable);
        var response = await client.PostAsJsonAsync("/launches", new LaunchRequest("Pioneer-11"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Préflight indisponible", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(sim.StartedFlights);
    }

    [Fact]
    public async Task Unknown_probe_returns_404()
    {
        var (client, sim, _) = Create(PreflightVerdict.Go, probeExists: false);
        var response = await client.PostAsJsonAsync("/launches", new LaunchRequest("Ghost"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(sim.StartedFlights);
    }

    [Fact]
    public async Task Force_storm_query_is_passed_to_the_runner_and_defaults_to_false()
    {
        var (client, _, preflight) = Create(PreflightVerdict.Go);
        await client.PostAsJsonAsync("/launches?forceStorm=true", new LaunchRequest("Pioneer-11"), TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync("/launches", new LaunchRequest("Pioneer-11"), TestContext.Current.CancellationToken);
        Assert.Equal([true, false], preflight.ForceStormCalls);
    }
}
