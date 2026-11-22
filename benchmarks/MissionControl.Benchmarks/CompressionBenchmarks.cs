using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using MissionControl.Contracts;

namespace MissionControl.Benchmarks;

/// <summary>Colonne "Taille" : octets compressés par méthode.</summary>
public sealed class CompressedSizeColumn : IColumn
{
    public string Id => nameof(CompressedSizeColumn);
    public string ColumnName => "Taille";
    public string Legend => "Taille compressée (octets)";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Size;
    public bool IsAvailable(Summary summary) => true;
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase) =>
        CompressionBenchmarks.CompressedSize(benchmarkCase.Descriptor.WorkloadMethod.Name)
            is { } size ? $"{size} o" : "-";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) =>
        GetValue(summary, benchmarkCase);
}

public sealed class SizeConfig : ManualConfig
{
    public SizeConfig() => AddColumn(new CompressedSizeColumn());
}

[MemoryDiagnoser]
[Config(typeof(SizeConfig))]
public class CompressionBenchmarks
{
    // 1 000 trames déterministes, encodées en CBOR puis concaténées.
    private static readonly byte[] Sample = BuildSample();

    private static byte[] BuildSample()
    {
        var baseTime = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        using var ms = new MemoryStream();
        for (var id = 1; id <= 1000; id++)
        {
            var frame = new TelemetryFrame(id, "Pioneer-11", baseTime.AddSeconds(id),
                AltitudeKm: 410 + 0.1 * id,
                VelocityKmS: 7.66 + 0.001 * id,
                BatteryPercent: Math.Max(0, 100 - 0.05 * id));
            ms.Write(TelemetryCbor.Encode(frame));
        }
        return ms.ToArray();
    }

    private static MemoryStream Run(string method)
    {
        var output = new MemoryStream();
        using (Stream s = method switch
        {
            nameof(GZip) => new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true),
            nameof(Brotli) => new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true),
            nameof(Zstandard) => new ZstandardStream(output, CompressionMode.Compress, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        })
        {
            s.Write(Sample);
        }
        return output;
    }

    public static long? CompressedSize(string method) =>
        method is nameof(GZip) or nameof(Brotli) or nameof(Zstandard) ? Run(method).Length : null;

    [Benchmark(Baseline = true)]
    public long GZip() => Run(nameof(GZip)).Length;

    [Benchmark]
    public long Brotli() => Run(nameof(Brotli)).Length;

    [Benchmark]
    public long Zstandard() => Run(nameof(Zstandard)).Length;
}
