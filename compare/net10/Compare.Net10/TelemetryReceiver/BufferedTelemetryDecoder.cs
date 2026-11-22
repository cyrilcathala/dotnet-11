using System.Formats.Cbor;
using MissionControl.Contracts;

namespace TelemetryReceiver;

/// <summary>
/// Décodeur CBOR « avant » : sans SlideData, on met tout le corps en tampon puis on décode d'un bloc.
/// Aucune trame n'est disponible avant la fin du flux.
/// </summary>
public sealed class BufferedTelemetryDecoder
{
    public static async Task<IReadOnlyList<TelemetryFrame>> DecodeAsync(Stream body, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await body.CopyToAsync(buffer, ct);

        var reader = new CborReader(buffer.ToArray(), CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
        var frames = new List<TelemetryFrame>();
        while (reader.PeekState() != CborReaderState.Finished)
            frames.Add(TelemetryCbor.Read(reader));
        return frames;
    }
}
