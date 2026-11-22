using System.Formats.Cbor;
using System.IO.Compression;
using Microsoft.Extensions.Time.Testing;
using MissionControl.Contracts;
using ProbeSimulator;

namespace ProbeSimulator.Tests;

public sealed class TelemetryContentTests
{
    [Fact]
    public async Task TelemetryContent_is_zstd_compressed_cbor_with_retransmissions()
    {
        var time = new FakeTimeProvider();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var frames = FlightTelemetry.Generate(new Flight("Voyager-X"), time, cts.Token).Take(7);
        var content = new TelemetryContent(frames);

        var ticker = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                time.Advance(TimeSpan.FromMilliseconds(500));
                await Task.Yield();
            }
        }, TestContext.Current.CancellationToken);

        using var compressed = new MemoryStream();
        await content.CopyToAsync(compressed, cts.Token);
        await cts.CancelAsync();
        await ticker;

        Assert.Equal("zstd", content.Headers.ContentEncoding.Single());
        Assert.Equal("application/cbor", content.Headers.ContentType?.MediaType);

        compressed.Position = 0;
        await using var zstd = new ZstandardStream(compressed, CompressionMode.Decompress);
        using var plain = new MemoryStream();
        await zstd.CopyToAsync(plain, TestContext.Current.CancellationToken);

        var reader = new CborReader(plain.ToArray(), CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        var decoded = new List<TelemetryFrame>();
        while (reader.PeekState() != CborReaderState.Finished)
            decoded.Add(TelemetryCbor.Read(reader));

        Assert.Equal(new long[] { 1, 2, 3, 4, 5, 5, 6 }, decoded.Select(f => f.PacketId));
        Assert.All(decoded, f => Assert.Equal("Voyager-X", f.ProbeName));
        Assert.Equal(410 + 0.1 * 1, decoded[0].AltitudeKm, 6);
    }
}
