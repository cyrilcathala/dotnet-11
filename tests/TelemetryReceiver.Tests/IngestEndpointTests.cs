using System.Formats.Cbor;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MissionControl.Contracts;
using TelemetryReceiver;

namespace TelemetryReceiver.Tests;

public sealed class IngestEndpointTests
{
    private sealed class ZstdCborContent : HttpContent
    {
        private readonly int _frames;

        public ZstdCborContent(int frames)
        {
            _frames = frames;
            Headers.ContentType = new MediaTypeHeaderValue("application/cbor");
            Headers.ContentEncoding.Add("zstd");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await using var zstd = new ZstandardStream(stream, CompressionMode.Compress, leaveOpen: true);
            var writer = new CborWriter(CborConformanceMode.Lax, false, allowMultipleRootLevelValues: true);
            for (var id = 1; id <= _frames; id++)
            {
                TelemetryCbor.Write(writer, DecoderTests.Frame(id));
                await zstd.WriteAsync(writer.Encode());
                writer.Reset();
                await zstd.FlushAsync();
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [Fact]
    public async Task Ingest_accepts_zstd_cbor_and_exposes_latest()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/telemetry/Pioneer-11", new ZstdCborContent(6), ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var latest = await client.GetFromJsonAsync<TelemetryFrame>("/telemetry/Pioneer-11/latest", ct);
        Assert.Equal(6, latest!.PacketId);
        var stats = await client.GetFromJsonAsync<TelemetryStats>("/telemetry/stats", ct);
        Assert.Equal(new TelemetryStats(6, 0), stats);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/telemetry/Nobody/latest", ct)).StatusCode);
    }

    [Fact]
    public async Task Ingest_records_raw_bytes_in_flight_recorder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        await client.PostAsync("/telemetry/Pioneer-11", new ZstdCborContent(3), ct);

        var recorder = (InMemoryFlightRecorder)factory.Services.GetRequiredService<IFlightRecorder>();
        Assert.NotEmpty(recorder.Snapshot("Pioneer-11"));
        Assert.Equal(DecoderTests.Sequence(1, 2, 3), recorder.Snapshot("Pioneer-11"));
    }

    /// <summary>Corps brut qui livre des octets puis lève une IOException, comme une connexion coupée.</summary>
    private sealed class DroppingBody(byte[] bytes) : Stream
    {
        private int _position;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_position >= bytes.Length)
                throw new IOException("connection lost");
            var n = Math.Min(Math.Min(buffer.Length, 11), bytes.Length - _position);
            bytes.AsMemory(_position, n).CopyTo(buffer);
            _position += n;
            return ValueTask.FromResult(n);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Aborted_upload_keeps_decoded_frames_and_does_not_500()
    {
        using var factory = new WebApplicationFactory<Program>();
        // Quatre trames complètes, puis la connexion tombe en plein milieu de la cinquième.
        var fifth = TelemetryCbor.Encode(DecoderTests.Frame(5));
        var body = DecoderTests.Sequence(1, 2, 3, 4).Concat(fifth.Take(10)).ToArray();
        var context = await factory.Server.SendAsync(c =>
        {
            c.Request.Method = "POST";
            c.Request.Path = "/telemetry/Pioneer-11";
            c.Request.Body = new DroppingBody(body);
        }, TestContext.Current.CancellationToken);

        Assert.NotEqual(500, context.Response.StatusCode);
        var store = factory.Services.GetRequiredService<TelemetryStore>();
        Assert.Equal(4, store.Latest("Pioneer-11")!.PacketId);
        Assert.Equal(4, store.Stats.Received);
    }
}
