using System.Net;
using System.Text;
using System.Text.Json;
using MissionControl.Contracts;

namespace MissionControl.Api.Tests;

public sealed class CommandsEndpointTests
{
    [Fact]
    public async Task Post_commands_deserializes_type_discriminator()
    {
        var simulator = FakeSimulatorHandler.Replying("ack");
        var client = TestApp.Create(simulator);
        var id = Guid.NewGuid();
        var json = $$"""{"type":"rotate","id":"{{id}}","probeName":"Pioneer-11","degrees":90}""";

        var response = await client.PostAsync("/commands", new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (path, body) = Assert.Single(simulator.Requests);
        Assert.Equal("/flights/Pioneer-11/commands", path);
        Assert.Equal(new SimulatorCommandRequest(id, "rotate", 90), body);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(id, result.RootElement.GetProperty("commandId").GetGuid());
        Assert.True(result.RootElement.TryGetProperty("at", out _));
    }

    [Fact]
    public async Task OpenApi_describes_CommandResult_with_anyOf()
    {
        var client = TestApp.Create(FakeSimulatorHandler.Replying("ack"));

        var openApiJson = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Contains("anyOf", openApiJson);
        using var doc = JsonDocument.Parse(openApiJson);
        var ok = doc.RootElement.GetProperty("paths").GetProperty("/commands").GetProperty("post")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString();
        Assert.Equal("#/components/schemas/CommandResult", ok);
        var cases = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CommandResult")
            .GetProperty("anyOf").EnumerateArray().Select(c => c.GetProperty("$ref").GetString());
        Assert.Equal(
            ["Acknowledged", "Rejected", "TimedOut", "ProbeLost"],
            cases.Select(r => r!["#/components/schemas/".Length..]));
    }
}
