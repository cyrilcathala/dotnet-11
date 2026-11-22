using System.Buffers;

namespace TelemetryReceiver;

/// <summary>Version .NET 10 de POST /telemetry/{probe} (src/TelemetryReceiver/Program.cs).</summary>
public static class IngestEndpoint
{
    public static IEndpointRouteBuilder MapIngest(this IEndpointRouteBuilder app)
    {
        app.MapPost("/telemetry/{probe}", async (string probe, HttpContext http, IFlightRecorder recorder) =>
        {
            var ct = http.RequestAborted;
            var reader = http.Request.BodyReader;
            using var body = new MemoryStream(); // pas de SlideData : on décode tout à la fin
            while (true)
            {
                var result = await reader.ReadAsync(ct);
                var buffer = result.Buffer;

                // Boîte noire. .NET 10 : pas de ReadOnlySequenceStream, on copie le tampon du pipe dans un tableau.
                await recorder.AppendAsync(probe, new MemoryStream(buffer.ToArray()), ct);
                foreach (var segment in buffer)
                    body.Write(segment.Span);

                reader.AdvanceTo(buffer.End);
                if (result.IsCompleted)
                    break;
            }

            body.Position = 0;
            var frames = await BufferedTelemetryDecoder.DecodeAsync(body, ct);
            return Results.Accepted(value: frames.Count);
        });
        return app;
    }
}
