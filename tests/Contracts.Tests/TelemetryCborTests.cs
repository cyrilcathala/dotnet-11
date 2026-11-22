using System.Formats.Cbor;
using MissionControl.Contracts;

namespace Contracts.Tests;

public class TelemetryCborTests
{
    private static TelemetryFrame SampleFrame() =>
        new(42, "Voyager-3", DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000), 410.5, 7.66, 88.0);

    [Fact]
    public void Encode_then_Read_round_trips_all_fields()
    {
        var frame = SampleFrame();
        var reader = new CborReader(TelemetryCbor.Encode(frame));
        Assert.Equal(frame, TelemetryCbor.Read(reader));
    }

    [Fact]
    public void Encode_writes_a_six_entry_map()
        => Assert.Equal(0xA6, TelemetryCbor.Encode(SampleFrame())[0]);

    [Fact]
    public void Read_accepts_keys_in_any_order()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(6);
        writer.WriteTextString("bat"); writer.WriteDouble(88.0);
        writer.WriteTextString("vel"); writer.WriteDouble(7.66);
        writer.WriteTextString("alt"); writer.WriteDouble(410.5);
        writer.WriteTextString("ts"); writer.WriteInt64(1_800_000_000_000);
        writer.WriteTextString("probe"); writer.WriteTextString("Voyager-3");
        writer.WriteTextString("id"); writer.WriteInt64(42);
        writer.WriteEndMap();

        Assert.Equal(SampleFrame(), TelemetryCbor.Read(new CborReader(writer.Encode())));
    }
}
