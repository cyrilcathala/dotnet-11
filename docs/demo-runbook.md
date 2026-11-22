# Runbook de démo

## Mesures

Nombre de frames de `GET /diagnostics/countdown-stack` (LaunchControl, .NET 11.0.100, build Debug, `dotnet LaunchControl.dll` puis `curl`) :

| Configuration | Frames |
|---|---|
| Sans runtime async (`runtime-async=off`) | 14 |
| Avec `<Features>runtime-async=on</Features>` | 7 |
| .NET 10.0.403, async classique (`compare/net10`, `dotnet run -- countdown`, console) | 13 |

Frames = lignes de la pile commençant par `   at `. Mesuré en retirant puis remettant la propriété dans `LaunchControl.csproj`.

## Benchmarks

`dotnet run -c Release --project benchmarks/MissionControl.Benchmarks -- --filter * --job short` (.NET 11.0.0, AMD Ryzen 7 5800X, BenchmarkDotNet 0.16.0-preview.2, ShortRun : 3 itérations, 1 lancement, 3 warmups). Niveau de compression par défaut : `CompressionLevel.Optimal` pour GZip et Brotli, constructeur par défaut `ZstandardStream(Stream, CompressionMode.Compress)` pour Zstd. Échantillon : 1 000 trames CBOR concaténées. Les baselines tournent aussi en `net11.0`.

### Compression

| Method    | Mean     | Error     | StdDev  | Ratio | Taille  | Gen0   | Allocated | Alloc Ratio |
|---------- |---------:|----------:|--------:|------:|--------:|-------:|----------:|------------:|
| GZip      | 622.0 μs |  71.22 μs | 3.90 μs |  1.00 | 17240 o | 2.9297 |  56.71 KB |        1.00 |
| Brotli    | 585.8 μs | 101.11 μs | 5.54 μs |  0.94 | 14824 o |      - |  14.68 KB |        0.26 |
| Zstandard | 311.0 μs |  12.98 μs | 0.71 μs |  0.50 | 14006 o | 0.4883 |  13.95 KB |        0.25 |

### Flux sans copie (64 Ko, 16 segments, lecture par 4 Ko)

| Method                 | Mean     | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|----------------------- |---------:|----------:|----------:|------:|-------:|----------:|------------:|
| MemoryStreamToArray    | 2.820 us | 1.3749 us | 0.0754 us |  1.00 | 3.9063 |   65624 B |       1.000 |
| ReadOnlySequenceStream | 1.217 us | 0.2876 us | 0.0158 us |  0.43 | 0.0038 |      80 B |       0.001 |
