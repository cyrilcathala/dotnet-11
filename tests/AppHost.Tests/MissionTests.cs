using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AppHost.Tests;

public class MissionTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan TelemetryTimeout = TimeSpan.FromSeconds(15);

    private static readonly string[] Services =
        ["probefactory", "launchcontrol", "probesimulator", "telemetryreceiver", "missioncontrol", "mcp"];

    [Fact(Timeout = 600_000)]
    public async Task Full_mission()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MissionControl_AppHost>(ct);
        // Journaux des ressources et des clients HTTP : seulement les avertissements.
        appHost.Services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Warning)
            .AddFilter(appHost.Environment.ApplicationName, LogLevel.Warning)
            .AddFilter("System.Net.Http", LogLevel.Warning));
        await using var app = await appHost.BuildAsync(ct);
        await app.StartAsync(ct).WaitAsync(StartTimeout, ct);
        foreach (var name in Services)
            await app.ResourceNotifications.WaitForResourceHealthyAsync(name, ct).WaitAsync(StartTimeout, ct);

        using var factory = app.CreateHttpClient("probefactory");
        using var launch = app.CreateHttpClient("launchcontrol");
        using var receiver = app.CreateHttpClient("telemetryreceiver");
        using var mission = app.CreateHttpClient("missioncontrol");

        // Le premier lancement compile preflight.cs : on laisse du temps.
        launch.Timeout = TimeSpan.FromMinutes(3);

        using var created = await factory.PostAsJsonAsync("/probes", new { name = "Pioneer-11", kind = "Orbiter" }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var noGo = await launch.PostAsJsonAsync("/launches?forceStorm=true", new { probeName = "Pioneer-11" }, ct);
        Assert.Equal(HttpStatusCode.Conflict, noGo.StatusCode);

        using var go = await launch.PostAsJsonAsync("/launches", new { probeName = "Pioneer-11" }, ct);
        Assert.Equal(HttpStatusCode.OK, go.StatusCode);

        await PollUntilAsync(async () =>
        {
            using var latest = await receiver.GetAsync("/telemetry/Pioneer-11/latest", ct);
            return latest.StatusCode == HttpStatusCode.OK;
        }, ct);

        // La 5e trame est retransmise (une trame toutes les 500 ms) : on attend le doublon
        // avant l'autodestruction, qui arrête le flux.
        await PollUntilAsync(async () => await DuplicatesAsync(receiver, ct) > 0, ct);

        using var command = await mission.PostAsJsonAsync("/commands",
            // "$type" n'est pas un nom de membre C# : le corps passe par un dictionnaire.
            new Dictionary<string, object> { ["$type"] = "SelfDestruct", ["id"] = Guid.NewGuid(), ["probeName"] = "Pioneer-11" }, ct);
        Assert.Equal(HttpStatusCode.OK, command.StatusCode);
        Assert.Contains("probeName", await command.Content.ReadAsStringAsync(ct));

        var history = await mission.GetStringAsync("/commands/history", ct);
        Assert.Single(history.Split('\n', StringSplitOptions.RemoveEmptyEntries));

        Assert.True(await DuplicatesAsync(receiver, ct) > 0);
    }

    private static async Task<long> DuplicatesAsync(HttpClient receiver, CancellationToken ct)
    {
        using var stats = JsonDocument.Parse(await receiver.GetStringAsync("/telemetry/stats", ct));
        return stats.RootElement.GetProperty("duplicates").GetInt64();
    }

    private static async Task PollUntilAsync(Func<Task<bool>> condition, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TelemetryTimeout);
        while (!await condition())
            await Task.Delay(250, timeout.Token);
    }
}
