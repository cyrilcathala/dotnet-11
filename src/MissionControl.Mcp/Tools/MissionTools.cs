using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace MissionControl.Mcp.Tools;

[McpServerToolType]
public class MissionTools(IHttpClientFactory httpClientFactory)
{
    private static readonly string[] CommandTypes = ["thrust", "rotate", "capture-photo", "self-destruct"];

    [McpServerTool]
    [Description("Lance une sonde déjà construite, après vérification météo.")]
    public Task<string> LaunchProbe(
        [Description("Nom de la sonde à lancer")] string probeName,
        CancellationToken ct = default) =>
        Guard("contrôle de lancement", async () =>
        {
            using var response = await httpClientFactory.CreateClient("launchcontrol")
                .PostAsJsonAsync("/launches", new { probeName }, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return $"Sonde inconnue : {probeName}.";
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                return "Préflight indisponible, réessayez plus tard.";
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict))
                return $"Réponse inattendue du contrôle de lancement : {(int)response.StatusCode}.";

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var weather = body.TryGetProperty("weather", out var w) ? w.ToString() : "";
            return response.StatusCode == HttpStatusCode.OK
                ? $"GO : la sonde {probeName} est lancée. Météo : {weather}"
                : $"NO-GO : lancement de {probeName} refusé. Météo : {weather}";
        });

    [McpServerTool]
    [Description("Envoie une commande à une sonde en vol : thrust, rotate, capture-photo ou self-destruct.")]
    public Task<string> SendCommand(
        [Description("Nom de la sonde")] string probeName,
        [Description("Type de commande : thrust, rotate, capture-photo ou self-destruct")] string type,
        [Description("Valeur : deltaV pour thrust, degrés pour rotate, ignorée sinon")] double? value = null,
        CancellationToken ct = default)
    {
        if (!CommandTypes.Contains(type))
            return Task.FromResult($"Type de commande inconnu : {type}. Types valides : {string.Join(", ", CommandTypes)}.");
        if (type is "thrust" or "rotate" && value is null)
            return Task.FromResult($"Commande {type} impossible : valeur requise.");

        return Guard("contrôle de mission", async () =>
        {
            var command = new Dictionary<string, object?>
            {
                ["type"] = type,
                ["id"] = Guid.NewGuid(),
                ["probeName"] = probeName
            };
            if (type == "thrust") command["deltaV"] = value;
            if (type == "rotate") command["degrees"] = value;

            using var response = await httpClientFactory.CreateClient("missioncontrol")
                .PostAsJsonAsync("/commands", command, ct);
            if (!response.IsSuccessStatusCode)
                return $"Réponse inattendue du contrôle de mission : {(int)response.StatusCode}.";
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (body.ValueKind != JsonValueKind.Object)
                return UnexpectedMission;

            if (body.TryGetProperty("reason", out var reason))
                return reason.ValueKind == JsonValueKind.String
                    ? $"Commande rejetée : {reason.GetString()}"
                    : "Commande rejetée (raison non précisée)";
            if (body.TryGetProperty("after", out _))
                return "Délai dépassé : la sonde n'a pas répondu à temps.";
            if (body.TryGetProperty("probeName", out var lost) && lost.ValueKind == JsonValueKind.String)
                return $"Contact perdu avec la sonde {lost.GetString()}.";
            if (body.TryGetProperty("at", out _))
                return "Commande acquittée.";
            return UnexpectedMission;
        });
    }

    [McpServerTool]
    [Description("Donne la dernière télémétrie reçue d'une sonde.")]
    public Task<string> GetTelemetry(
        [Description("Nom de la sonde")] string probeName,
        CancellationToken ct = default) =>
        Guard("récepteur de télémétrie", async () =>
        {
            using var response = await httpClientFactory.CreateClient("telemetryreceiver")
                .GetAsync($"/telemetry/{Uri.EscapeDataString(probeName)}/latest", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return $"Aucune télémétrie reçue pour la sonde {probeName}.";
            if (!response.IsSuccessStatusCode)
                return $"Réponse inattendue du récepteur de télémétrie : {(int)response.StatusCode}.";

            var frame = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var inv = CultureInfo.InvariantCulture;
            string Value(string name) => frame.GetProperty(name).GetDouble().ToString("0.##", inv);
            return $"Sonde {probeName} : altitude {Value("altitudeKm")} km, "
                + $"vitesse {Value("velocityKmS")} km/s, batterie {Value("batteryPercent")} %.";
        });

    private const string UnexpectedMission = "Réponse inattendue du contrôle de mission.";

    private static async Task<string> Guard(string service, Func<Task<string>> call)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException)
        {
            return $"Service {service} injoignable.";
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return $"Réponse inattendue du {service}.";
        }
    }
}
