using System.Buffers;
using MissionControl.Contracts;
using TelemetryReceiver;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRequestDecompression();
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<IFlightRecorder, InMemoryFlightRecorder>();

var app = builder.Build();
app.UseRequestDecompression();
app.MapDefaultEndpoints();

app.MapPost("/telemetry/{probe}", async (string probe, HttpContext http, TelemetryStore store,
    IFlightRecorder recorder, ILogger<Program> logger) =>
{
    var ct = http.RequestAborted;
    var reader = http.Request.BodyReader;
    var decoder = new IncrementalTelemetryDecoder();
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
        logger.LogWarning(e, "Envoi de télémétrie de {Probe} interrompu ; les trames déjà décodées sont conservées", probe);
    }
    return Results.Accepted();
});

app.MapGet("/telemetry/stats", (TelemetryStore store) => store.Stats);

app.MapGet("/telemetry/{probe}/latest", (string probe, TelemetryStore store) =>
    store.Latest(probe) is { } frame ? Results.Ok(frame) : Results.NotFound());

app.Run();

public partial class Program;
