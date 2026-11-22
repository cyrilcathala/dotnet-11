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
        Assert.True(body.RuntimeAsync);
        Assert.Contains("OrbitInsertionAsync", body.Stack);
    }

    [Fact]
    public async Task Same_source_without_runtime_async_has_a_deeper_live_stack()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var with = await client.GetFromJsonAsync<CountdownStackResponse>("/diagnostics/countdown-stack?runtimeAsync=true", ct);
        var without = await client.GetFromJsonAsync<CountdownStackResponse>("/diagnostics/countdown-stack?runtimeAsync=false", ct);

        Assert.NotNull(with);
        Assert.NotNull(without);
        Assert.False(without.RuntimeAsync);
        Assert.Contains("OrbitInsertionAsync", without.Stack);
        // Sans runtime async, chaque méthode async ajoute ses frames de machine à états (MoveNext, AsyncMethodBuilder…).
        Assert.True(without.Frames > with.Frames, $"sans : {without.Frames}, avec : {with.Frames}");
    }
}
