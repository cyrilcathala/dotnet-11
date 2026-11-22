using System.Formats.Cbor;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using MissionControl.Contracts;

namespace ProbeSimulator;

/// <summary>Corps HTTP en streaming : suite CBOR de trames compressée en Zstd, flush après chaque trame.</summary>
public sealed class TelemetryContent : HttpContent
{
    private readonly IAsyncEnumerable<TelemetryFrame> _frames;

    public TelemetryContent(IAsyncEnumerable<TelemetryFrame> frames)
    {
        _frames = frames;
        Headers.ContentType = new MediaTypeHeaderValue("application/cbor");
        Headers.ContentEncoding.Add("zstd");
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct)
    {
        await using var zstd = new ZstandardStream(stream, CompressionMode.Compress, leaveOpen: true);
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false,
            allowMultipleRootLevelValues: true);

        await foreach (var frame in _frames.WithCancellation(ct))
        {
            TelemetryCbor.Write(writer, frame);
            await zstd.WriteAsync(writer.Encode(), ct);
            writer.Reset();
            await zstd.FlushAsync(ct);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
