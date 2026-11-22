using System.Formats.Cbor;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using MissionControl.Contracts;

namespace ProbeSimulator;

/// <summary>Corps HTTP en streaming : suite CBOR de trames compressée en Brotli (pas de Zstandard en .NET 10), flush après chaque trame.</summary>
public sealed class TelemetryContent : HttpContent
{
    private readonly IAsyncEnumerable<TelemetryFrame> _frames;

    public TelemetryContent(IAsyncEnumerable<TelemetryFrame> frames)
    {
        _frames = frames;
        Headers.ContentType = new MediaTypeHeaderValue("application/cbor");
        Headers.ContentEncoding.Add("br");
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct)
    {
        await using var brotli = new BrotliStream(stream, CompressionMode.Compress, leaveOpen: true);
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false,
            allowMultipleRootLevelValues: true);

        await foreach (var frame in _frames.WithCancellation(ct))
        {
            TelemetryCbor.Write(writer, frame);
            await brotli.WriteAsync(writer.Encode(), ct);
            writer.Reset();
            await brotli.FlushAsync(ct);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
