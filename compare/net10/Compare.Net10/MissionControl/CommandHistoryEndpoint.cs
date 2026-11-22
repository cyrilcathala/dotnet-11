using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace MissionControl.Api;

public sealed record CommandHistoryEntry(Guid CommandId, string ProbeName, string Command, string Outcome, DateTimeOffset At);

public interface ICommandHistory
{
    IAsyncEnumerable<CommandHistoryEntry> ReadAllAsync(CancellationToken ct);
}

public static class CommandHistoryEndpoint
{
    public static IEndpointRouteBuilder MapCommandHistory(this IEndpointRouteBuilder app)
    {
        app.MapGet("/commands/history", async (HttpResponse response, ICommandHistory history,
            IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            response.ContentType = "application/jsonl";
            // .NET 10 : pas de topLevelValues, on sérialise chaque entrée puis on écrit le saut de ligne soi-même.
            await foreach (var entry in history.ReadAllAsync(ct))
            {
                await JsonSerializer.SerializeAsync(response.Body, entry, json.Value.SerializerOptions, ct);
                await response.Body.WriteAsync("\n"u8.ToArray(), ct);
            }
        });
        return app;
    }
}
