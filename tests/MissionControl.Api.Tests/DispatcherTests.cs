using System.Net;
using Microsoft.Extensions.Options;
using MissionControl.Contracts;

namespace MissionControl.Api.Tests;

public sealed class DispatcherTests
{
    private static CommandDispatcher Create(FakeSimulatorHandler simulator, TimeSpan? timeout = null) =>
        new(new FakeClientFactory(simulator),
            Options.Create(new CommandDispatcherOptions { CommandTimeout = timeout ?? TimeSpan.FromSeconds(2) }),
            TimeProvider.System);

    [Fact]
    public async Task Ack_from_simulator_is_Acknowledged()
    {
        var simulator = FakeSimulatorHandler.Replying("ack");
        var id = Guid.NewGuid();

        var result = await Create(simulator).DispatchAsync(new Thrust(id, "Pioneer-11", 3.5), TestContext.Current.CancellationToken);

        var ack = Assert.IsType<Acknowledged>(result.Value);
        Assert.Equal(id, ack.CommandId);
        var (path, body) = Assert.Single(simulator.Requests);
        Assert.Equal("/flights/Pioneer-11/commands", path);
        Assert.Equal(new SimulatorCommandRequest(id, "thrust", 3.5), body);
    }

    [Fact]
    public async Task Self_destruct_yields_ProbeLost()
    {
        var simulator = FakeSimulatorHandler.Replying("lost");
        var id = Guid.NewGuid();

        var result = await Create(simulator).DispatchAsync(new SelfDestruct(id, "Pioneer-11"), TestContext.Current.CancellationToken);

        Assert.Equal(new ProbeLost(id, "Pioneer-11"), Assert.IsType<ProbeLost>(result.Value));
        Assert.Equal(new SimulatorCommandRequest(id, "self-destruct", null), Assert.Single(simulator.Requests).Body);
    }

    [Fact]
    public async Task Rejected_from_simulator_keeps_the_reason()
    {
        var simulator = FakeSimulatorHandler.Replying("rejected", "Batterie trop faible");
        var id = Guid.NewGuid();

        var result = await Create(simulator).DispatchAsync(new CapturePhoto(id, "Pioneer-11"), TestContext.Current.CancellationToken);

        Assert.Equal(new Rejected(id, "Batterie trop faible"), Assert.IsType<Rejected>(result.Value));
        Assert.Equal(new SimulatorCommandRequest(id, "capture-photo", null), Assert.Single(simulator.Requests).Body);
    }

    [Fact]
    public async Task Unknown_probe_yields_Rejected_not_500()
    {
        var simulator = new FakeSimulatorHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await Create(simulator).DispatchAsync(new Rotate(Guid.NewGuid(), "Inconnue", 90), TestContext.Current.CancellationToken);

        var rejected = Assert.IsType<Rejected>(result.Value);
        Assert.Equal("Sonde inconnue ou au sol", rejected.Reason);
    }

    [Fact]
    public async Task Simulator_5xx_yields_Rejected_not_500()
    {
        var simulator = new FakeSimulatorHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var id = Guid.NewGuid();

        var result = await Create(simulator).DispatchAsync(new Thrust(id, "Pioneer-11", 1), TestContext.Current.CancellationToken);

        Assert.Equal(new Rejected(id, "Simulateur indisponible"), Assert.IsType<Rejected>(result.Value));
    }

    [Fact]
    public async Task Unreachable_simulator_yields_Rejected_not_500()
    {
        var simulator = new FakeSimulatorHandler((_, _) => throw new HttpRequestException("Connexion refusée"));
        var id = Guid.NewGuid();

        var result = await Create(simulator).DispatchAsync(new Thrust(id, "Pioneer-11", 1), TestContext.Current.CancellationToken);

        Assert.Equal(new Rejected(id, "Simulateur indisponible"), Assert.IsType<Rejected>(result.Value));
    }

    [Fact]
    public async Task Slow_simulator_yields_TimedOut()
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var simulator = new FakeSimulatorHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return FakeSimulatorHandler.Outcome("ack");
        });
        var id = Guid.NewGuid();

        var result = await Create(simulator, timeout).DispatchAsync(new Thrust(id, "Pioneer-11", 1), TestContext.Current.CancellationToken);

        Assert.Equal(new TimedOut(id, timeout), Assert.IsType<TimedOut>(result.Value));
    }

    [Fact]
    public void Describe_names_every_case()
    {
        var id = Guid.NewGuid();
        Assert.Equal("ack", CommandDispatcher.Describe(new Acknowledged(id, DateTimeOffset.UtcNow)));
        Assert.Equal("rejected", CommandDispatcher.Describe(new Rejected(id, "non")));
        Assert.Equal("timeout", CommandDispatcher.Describe(new TimedOut(id, TimeSpan.FromSeconds(2))));
        Assert.Equal("lost", CommandDispatcher.Describe(new ProbeLost(id, "Pioneer-11")));
    }
}
