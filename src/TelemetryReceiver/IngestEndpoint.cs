using System.Buffers;

namespace TelemetryReceiver;

/// <summary>Version .NET 11 de POST /telemetry/{probe} (compare/net10/Compare.Net10/TelemetryReceiver/IngestEndpoint.cs).</summary>
public static class IngestEndpoint
{
    public static IEndpointRouteBuilder MapIngest(this IEndpointRouteBuilder app)
    {
        app.MapPost("/telemetry/{probe}", async (string probe, HttpContext http, TelemetryStore store,
            IFlightRecorder recorder, ILoggerFactory loggers) =>
        {
            var ct = http.RequestAborted;
            var reader = http.Request.BodyReader;
            var decoder = new IncrementalTelemetryDecoder(); // SlideData : on décode au fil de l'eau
            try
            {
                while (true)
                {
                    var result = await reader.ReadAsync(ct);
                    var buffer = result.Buffer;

                    // Boîte noire : les octets bruts, sans copie intermédiaire du tampon du pipe.
                    await recorder.AppendAsync(probe, new ReadOnlySequenceStream(buffer), ct);
                    foreach (var segment in buffer)
                    {
                        foreach (var frame in decoder.Push(segment, isFinalBlock: false))
                            store.TryAdd(frame);
                    }

                    reader.AdvanceTo(buffer.End);
                    if (result.IsCompleted)
                        break;
                }
                foreach (var frame in decoder.Push(ReadOnlyMemory<byte>.Empty, isFinalBlock: true))
                    store.TryAdd(frame);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or BadHttpRequestException)
            {
                loggers.CreateLogger(typeof(IngestEndpoint)).LogWarning(e,
                    "Envoi de télémétrie de {Probe} interrompu ; les trames déjà décodées sont conservées", probe);
            }
            return Results.Accepted();
        });
        return app;
    }
}
