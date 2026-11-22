using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MissionControl.Contracts;
using ProbeSimulator;

namespace ProbeSimulator.Tests;

public sealed class FlightTests
{
    private static HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                var emitter = services.Single(d => d.ImplementationType == typeof(TelemetryEmitter));
                services.Remove(emitter);
            }));
        return factory.CreateClient();
    }

    private static Task<HttpResponseMessage> Start(HttpClient c, string probe) =>
        c.PostAsJsonAsync("/flights", new StartFlightRequest(probe), TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> Command(HttpClient c, string probe, string type, double? value = null) =>
        c.PostAsJsonAsync($"/flights/{probe}/commands", new SimulatorCommandRequest(Guid.NewGuid(), type, value), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Start_returns_202_and_is_idempotent_case_insensitive()
    {
        var client = CreateClient();
        Assert.Equal(HttpStatusCode.Accepted, (await Start(client, "Voyager-X")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await Start(client, "voyager-x")).StatusCode);
        var ack = await (await Command(client, "VOYAGER-X", "thrust", 1)).Content.ReadFromJsonAsync<SimulatorCommandOutcome>(TestContext.Current.CancellationToken);
        Assert.Equal("ack", ack!.Outcome);
    }

    [Fact]
    public async Task Self_destruct_returns_lost_then_every_command_returns_lost()
    {
        var client = CreateClient();
        await Start(client, "Voyager-X");

        var first = await (await Command(client, "Voyager-X", "self-destruct")).Content.ReadFromJsonAsync<SimulatorCommandOutcome>(TestContext.Current.CancellationToken);
        var second = await (await Command(client, "Voyager-X", "thrust", 2)).Content.ReadFromJsonAsync<SimulatorCommandOutcome>(TestContext.Current.CancellationToken);

        Assert.Equal("lost", first!.Outcome);
        Assert.Null(first.Reason);
        Assert.Equal("lost", second!.Outcome);
        Assert.Equal("Contact perdu", second.Reason);
    }

    [Fact]
    public async Task Negative_thrust_is_rejected()
    {
        var client = CreateClient();
        await Start(client, "Voyager-X");

        var outcome = await (await Command(client, "Voyager-X", "thrust", -1)).Content.ReadFromJsonAsync<SimulatorCommandOutcome>(TestContext.Current.CancellationToken);

        Assert.Equal("rejected", outcome!.Outcome);
        Assert.Equal("Poussée invalide", outcome.Reason);
    }

    [Fact]
    public async Task Command_for_unknown_flight_returns_404()
    {
        var client = CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await Command(client, "Nobody", "thrust", 1)).StatusCode);
    }
}
