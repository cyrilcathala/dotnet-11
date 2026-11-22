using MissionControl.Contracts;
using TelemetryReceiver;

namespace TelemetryReceiver.Tests;

public sealed class StoreTests
{
    [Fact]
    public void Duplicates_out_of_order_are_counted_once()
    {
        var store = new TelemetryStore();
        foreach (var id in new long[] { 1, 3, 2, 3, 1 })
            store.TryAdd(DecoderTests.Frame(id));
        Assert.Equal(new TelemetryStats(3, 2), store.Stats);
    }

    [Fact]
    public void Same_packet_id_on_different_probes_is_not_a_duplicate()
    {
        var store = new TelemetryStore();
        Assert.True(store.TryAdd(DecoderTests.Frame(1, "A")));
        Assert.True(store.TryAdd(DecoderTests.Frame(1, "B")));
        Assert.Equal(1, store.Latest("B")!.PacketId);
        Assert.Null(store.Latest("C"));
    }

    [Fact]
    public void Latest_is_the_most_recently_accepted_frame()
    {
        var store = new TelemetryStore();
        store.TryAdd(DecoderTests.Frame(5));
        store.TryAdd(DecoderTests.Frame(4));
        Assert.Equal(4, store.Latest("Pioneer-11")!.PacketId);
    }

    [Fact]
    public void Probe_names_are_case_insensitive()
    {
        var store = new TelemetryStore();
        Assert.True(store.TryAdd(DecoderTests.Frame(1, "Pioneer-11")));
        Assert.False(store.TryAdd(DecoderTests.Frame(1, "PIONEER-11")));
        Assert.Equal(1, store.Latest("pioneer-11")!.PacketId);
    }
}
