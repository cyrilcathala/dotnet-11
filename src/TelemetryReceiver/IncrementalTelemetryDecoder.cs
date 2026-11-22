using System.Formats.Cbor;
using MissionControl.Contracts;

namespace TelemetryReceiver;

/// <summary>
/// Décodeur CBOR incrémental : on lui donne des morceaux d'octets de taille quelconque, il rend les trames complètes.
/// Une trame incomplète attend le morceau suivant ; si elle est encore incomplète au bloc final, elle est abandonnée.
/// </summary>
public sealed class IncrementalTelemetryDecoder
{
    private static readonly CborReaderOptions Options = new()
    {
        ConformanceMode = CborConformanceMode.Lax,
        AllowMultipleRootLevelValues = true,
    };

    private readonly CborReader _reader = new(ReadOnlyMemory<byte>.Empty, Options, isFinalBlock: false);
    private byte[] _leftover = [];

    public IReadOnlyList<TelemetryFrame> Push(ReadOnlyMemory<byte> chunk, bool isFinalBlock)
    {
        // SlideData remplace le tampon : on y remet d'abord les octets non consommés du tour précédent.
        var data = new byte[_leftover.Length + chunk.Length];
        _leftover.CopyTo(data, 0);
        chunk.CopyTo(data.AsMemory(_leftover.Length));
        _reader.SlideData(data, isFinalBlock);

        var frames = new List<TelemetryFrame>();
        while (_reader.PeekState() is not (CborReaderState.NeedsMoreData or CborReaderState.Finished)
               && IsNextValueComplete(data))
        {
            frames.Add(TelemetryCbor.Read(_reader));
        }

        _leftover = data[^_reader.BytesRemaining..];
        return frames;
    }

    // Une trame à moitié arrivée se détecte par TrySkipValue(false) sur le lecteur jetable ; NeedsMoreData signifie seulement que le tampon est vide.
    // TrySkipValue(false) rend false tant que la valeur est incomplète, sans avancer : on l'essaie sur un lecteur jetable.
    private bool IsNextValueComplete(byte[] data)
    {
        var probe = new CborReader(data.AsMemory(data.Length - _reader.BytesRemaining), Options, isFinalBlock: false);
        return probe.TrySkipValue(false);
    }
}
