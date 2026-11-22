using System.IO.Compression;
using MissionControl.Contracts;
using TelemetryReceiver;

namespace TelemetryReceiver.Tests;

public sealed class DecoderTests
{
    internal static TelemetryFrame Frame(long id, string probe = "Pioneer-11") =>
        new(id, probe, DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000 + id), 100 + id, 7.5, 90);

    internal static byte[] Sequence(params long[] ids) =>
        ids.SelectMany(id => TelemetryCbor.Encode(Frame(id))).ToArray();

    [Fact]
    public void Decodes_frames_split_into_7_byte_chunks()
    {
        var bytes = Sequence(1, 2, 3);
        var decoder = new IncrementalTelemetryDecoder();
        var frames = new List<TelemetryFrame>();
        for (var i = 0; i < bytes.Length; i += 7)
        {
            var chunk = bytes.AsMemory(i, Math.Min(7, bytes.Length - i));
            frames.AddRange(decoder.Push(chunk, isFinalBlock: i + 7 >= bytes.Length));
        }
        Assert.Equal(new long[] { 1, 2, 3 }, frames.Select(f => f.PacketId));
        Assert.Equal(Frame(2), frames[1]);
    }

    [Fact]
    public void Drops_a_truncated_last_frame_without_throwing()
    {
        var third = TelemetryCbor.Encode(Frame(3));
        var all = Sequence(1, 2).Concat(third.Take(third.Length / 2)).ToArray();
        var frames = new IncrementalTelemetryDecoder().Push(all, isFinalBlock: true);
        Assert.Equal(2, frames.Count);
    }

    [Fact]
    public void Zstd_flush_makes_the_first_frame_decodable_before_the_second_exists()
    {
        var compressed = new MemoryStream();
        long afterFirstFlush;
        using (var zstd = new ZstandardStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            zstd.Write(TelemetryCbor.Encode(Frame(1)));
            zstd.Flush();
            afterFirstFlush = compressed.Length;
            zstd.Write(TelemetryCbor.Encode(Frame(2)));
            zstd.Flush();
        }
        Assert.True(afterFirstFlush > 0);
        Assert.True(compressed.Length > afterFirstFlush);

        // Only the bytes available after the first flush: frame 2 does not exist yet.
        var available = compressed.ToArray().AsSpan(0, (int)afterFirstFlush).ToArray();
        using var decompress = new ZstandardStream(new MemoryStream(available), CompressionMode.Decompress);
        var buffer = new byte[4096];
        var read = decompress.Read(buffer, 0, buffer.Length);
        Assert.True(read > 0);

        var frames = new IncrementalTelemetryDecoder().Push(buffer.AsMemory(0, read), isFinalBlock: false);
        Assert.Equal(new long[] { 1 }, frames.Select(f => f.PacketId));
    }
}
