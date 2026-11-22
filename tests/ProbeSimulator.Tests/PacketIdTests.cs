using Microsoft.Extensions.Time.Testing;
using MissionControl.Contracts;
using ProbeSimulator;

namespace ProbeSimulator.Tests;

/// <summary>Le récepteur déduplique par PacketId : les ids ne doivent jamais se répéter pour une sonde.</summary>
public sealed class PacketIdTests
{
    private static async Task<long> FirstPacketId(Flight flight)
    {
        var frame = await FlightTelemetry.Generate(flight, new FakeTimeProvider(), TestContext.Current.CancellationToken)
            .FirstAsync(TestContext.Current.CancellationToken);
        return frame.PacketId;
    }

    [Fact]
    public async Task Emitter_retry_continues_packet_ids()
    {
        var flight = new Flight("Voyager-X");

        Assert.Equal(1, await FirstPacketId(flight));
        Assert.Equal(2, await FirstPacketId(flight));
    }

    [Fact]
    public async Task Relaunch_after_self_destruct_continues_packet_ids()
    {
        var registry = new FlightRegistry();
        Assert.True(registry.Start("Voyager-X"));
        Assert.True(registry.Started.TryRead(out var first));
        Assert.Equal(1, await FirstPacketId(first));

        registry.Apply("Voyager-X", new SimulatorCommandRequest(Guid.NewGuid(), "self-destruct", null));
        Assert.True(registry.Start("voyager-x"));
        Assert.True(registry.Started.TryRead(out var second));

        Assert.NotSame(first, second);
        Assert.Equal(2, await FirstPacketId(second));
    }
}
