using System.Net;
using System.Text;
using System.Text.Json;
using MissionControl.Mcp.Tools;

namespace Mcp.Tests;

public class MissionToolsTests
{
    private sealed class FakeHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public string? SentBody;
        public Uri? SentUri;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            SentUri = request.RequestUri;
            if (request.Content is not null) SentBody = await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FakeFactory(FakeHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://test")
        };
    }

    private static (MissionTools Tools, FakeHandler Handler) Make(HttpStatusCode status, string json)
    {
        var handler = new FakeHandler(status, json);
        return (new MissionTools(new FakeFactory(handler)), handler);
    }

    [Fact]
    public async Task SendCommand_posts_the_type_discriminator()
    {
        var (tools, handler) = Make(HttpStatusCode.OK, """{"commandId":"c","at":"2026-01-01T00:00:00Z"}""");
        await tools.SendCommand("Voyager", "rotate", 90, ct: TestContext.Current.CancellationToken);
        var sent = JsonDocument.Parse(handler.SentBody!).RootElement;
        Assert.Equal("rotate", sent.GetProperty("type").GetString());
        Assert.Equal(90, sent.GetProperty("degrees").GetDouble());
        Assert.Equal("Voyager", sent.GetProperty("probeName").GetString());
        Assert.True(Guid.TryParse(sent.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task SendCommand_thrust_maps_value_to_deltaV()
    {
        var (tools, handler) = Make(HttpStatusCode.OK, """{"commandId":"c","at":"2026-01-01T00:00:00Z"}""");
        await tools.SendCommand("Voyager", "thrust", 1.5, ct: TestContext.Current.CancellationToken);
        Assert.Equal(1.5, JsonDocument.Parse(handler.SentBody!).RootElement.GetProperty("deltaV").GetDouble());
    }

    [Fact]
    public async Task SendCommand_unknown_type_is_rejected_without_calling_the_api()
    {
        var (tools, handler) = Make(HttpStatusCode.OK, "{}");
        var answer = await tools.SendCommand("Voyager", "dance", null, ct: TestContext.Current.CancellationToken);
        Assert.Contains("inconnu", answer);
        Assert.Null(handler.SentUri);
    }

    [Fact]
    public async Task SendCommand_acknowledged()
    {
        var (tools, _) = Make(HttpStatusCode.OK, """{"commandId":"c","at":"2026-01-01T00:00:00Z"}""");
        Assert.Contains("Commande acquittée", await tools.SendCommand("V", "capture-photo", null, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendCommand_rejected()
    {
        var (tools, _) = Make(HttpStatusCode.OK, """{"commandId":"c","reason":"batterie faible"}""");
        Assert.Contains("Commande rejetée : batterie faible", await tools.SendCommand("V", "self-destruct", null, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendCommand_timed_out()
    {
        var (tools, _) = Make(HttpStatusCode.OK, """{"commandId":"c","after":"00:00:05"}""");
        Assert.Contains("Délai dépassé", await tools.SendCommand("V", "thrust", 1, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendCommand_probe_lost()
    {
        var (tools, _) = Make(HttpStatusCode.OK, """{"commandId":"c","probeName":"Voyager"}""");
        Assert.Contains("Contact perdu avec la sonde Voyager", await tools.SendCommand("Voyager", "thrust", 1, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LaunchProbe_reports_go_with_weather()
    {
        var (tools, _) = Make(HttpStatusCode.OK, """{"probeName":"Voyager","status":"GO","weather":"Ciel dégagé"}""");
        var answer = await tools.LaunchProbe("Voyager", ct: TestContext.Current.CancellationToken);
        Assert.Contains("GO", answer);
        Assert.Contains("Ciel dégagé", answer);
    }

    [Fact]
    public async Task LaunchProbe_reports_no_go()
    {
        var (tools, _) = Make(HttpStatusCode.Conflict, """{"probeName":"Voyager","status":"NO-GO","weather":"Tempête"}""");
        Assert.Contains("NO-GO", await tools.LaunchProbe("Voyager", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LaunchProbe_unknown_probe()
    {
        var (tools, _) = Make(HttpStatusCode.NotFound, "{}");
        Assert.Contains("Sonde inconnue", await tools.LaunchProbe("X", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LaunchProbe_preflight_unavailable()
    {
        var (tools, _) = Make(HttpStatusCode.ServiceUnavailable, """{"detail":"Préflight indisponible"}""");
        Assert.Contains("Préflight indisponible", await tools.LaunchProbe("X", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetTelemetry_returns_values_with_units()
    {
        var (tools, _) = Make(HttpStatusCode.OK,
            """{"packetId":1,"probeName":"V","timestamp":"2026-01-01T00:00:00Z","altitudeKm":400.5,"velocityKmS":7.66,"batteryPercent":82}""");
        var answer = await tools.GetTelemetry("V", ct: TestContext.Current.CancellationToken);
        Assert.Contains("400.5 km", answer);
        Assert.Contains("7.66 km/s", answer);
        Assert.Contains("82 %", answer);
    }

    [Fact]
    public async Task GetTelemetry_for_silent_probe_says_so()
    {
        var (tools, _) = Make(HttpStatusCode.NotFound, "{}");
        Assert.Contains("Aucune télémétrie", await tools.GetTelemetry("V", ct: TestContext.Current.CancellationToken));
    }
}
