using System.Buffers;
using BenchmarkDotNet.Attributes;

namespace MissionControl.Benchmarks;

[MemoryDiagnoser]
public class StreamWrapBenchmarks
{
    private const int Total = 64 * 1024, Segments = 16;
    private ReadOnlySequence<byte> _seq;
    private readonly byte[] _buffer = new byte[4096];

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory, Segment? previous)
        {
            Memory = memory;
            if (previous is not null)
            {
                RunningIndex = previous.RunningIndex + previous.Memory.Length;
                previous.Next = this;
            }
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[Total];
        new Random(42).NextBytes(data);
        var size = Total / Segments;
        Segment? first = null, last = null;
        for (var i = 0; i < Segments; i++)
        {
            last = new Segment(data.AsMemory(i * size, size), last);
            first ??= last;
        }
        _seq = new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
    }

    private long Drain(Stream s)
    {
        long n = 0;
        int read;
        while ((read = s.Read(_buffer, 0, _buffer.Length)) > 0) n += read;
        return n;
    }

    [Benchmark(Baseline = true)]
    public long MemoryStreamToArray()
    {
        using var s = new MemoryStream(_seq.ToArray());
        return Drain(s);
    }

    [Benchmark]
    public long ReadOnlySequenceStream()
    {
        using var s = new ReadOnlySequenceStream(_seq);
        return Drain(s);
    }
}
