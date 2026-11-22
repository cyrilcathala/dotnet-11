using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LaunchControl.Tests;

public sealed class CountdownStackTests
{
    [Fact]
    public async Task Stack_names_every_countdown_step()
    {
        var stack = await new CountdownSequence().CaptureStackAsync(TestContext.Current.CancellationToken);
        Assert.Contains("OrbitInsertionAsync", stack);
        Assert.Contains("StageSeparationAsync", stack);
        Assert.Contains("IgnitionSequenceAsync", stack);
    }

    [Fact]
    public async Task Endpoint_returns_frame_count()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var body = await client.GetFromJsonAsync<CountdownStackResponse>("/diagnostics/countdown-stack", TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.True(body.Frames > 0);
        Assert.Contains("OrbitInsertionAsync", body.Stack);
    }
}
