using System.Net.Http.Json;
using System.Text.Json;
using MissionControl.Contracts;

namespace MissionControl.Api.Tests;

public sealed class HistoryEndpointTests
{
    [Fact]
    public async Task History_is_json_lines()
    {
        var simulator = new FakeSimulatorHandler((req, _) =>
            Task.FromResult(FakeSimulatorHandler.Outcome(req.Type == "self-destruct" ? "lost" : "ack")));
        var client = TestApp.Create(simulator);
        var ct = TestContext.Current.CancellationToken;
        var selfDestructId = Guid.NewGuid();

        ProbeCommand[] commands =
        [
            new Thrust(Guid.NewGuid(), "Pioneer-11", 2),
            new CapturePhoto(Guid.NewGuid(), "Pioneer-11", 2),
            new SelfDestruct(selfDestructId, "Pioneer-11"),
        ];
        foreach (var command in commands)
        {
            (await client.PostAsJsonAsync("/commands", command, ct)).EnsureSuccessStatusCode();
        }

        var resp = await client.GetAsync("/commands/history", ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        Assert.Equal("application/jsonl", resp.Content.Headers.ContentType!.MediaType);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, l => JsonDocument.Parse(l));
        using var last = JsonDocument.Parse(lines[2]);
        Assert.Equal(selfDestructId, last.RootElement.GetProperty("commandId").GetGuid());
        Assert.Equal("self-destruct", last.RootElement.GetProperty("command").GetString());
        Assert.Equal("lost", last.RootElement.GetProperty("outcome").GetString());
    }
}
