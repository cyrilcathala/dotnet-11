using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MissionControl.Contracts;

namespace MissionControl.Api.Tests;

/// <summary>Faux ProbeSimulator : enregistre les requêtes reçues et répond via <c>respond</c>.</summary>
internal sealed class FakeSimulatorHandler(Func<SimulatorCommandRequest, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    public List<(string Path, SimulatorCommandRequest Body)> Requests { get; } = [];

    public static FakeSimulatorHandler Replying(string outcome, string? reason = null) =>
        new((_, _) => Task.FromResult(Outcome(outcome, reason)));

    public static HttpResponseMessage Outcome(string outcome, string? reason = null) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new SimulatorCommandOutcome(outcome, reason)) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = (await request.Content!.ReadFromJsonAsync<SimulatorCommandRequest>(ct))!;
        Requests.Add((request.RequestUri!.AbsolutePath, body));
        return await respond(body, ct);
    }
}

internal sealed class FakeClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("http://probesimulator") };
}

internal static class TestApp
{
    public static HttpClient Create(FakeSimulatorHandler simulator) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
                s.AddHttpClient("probesimulator").ConfigurePrimaryHttpMessageHandler(() => simulator)))
            .CreateClient();
}
