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
    public async Task<string> LaunchProbe(
        [Description("Nom de la sonde à lancer")] string probeName,
        CancellationToken ct = default)
    {
        using var response = await httpClientFactory.CreateClient("launchcontrol")
            .PostAsJsonAsync("/launches", new { probeName }, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return $"Sonde inconnue : {probeName}.";
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            return "Préflight indisponible, réessayez plus tard.";

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var weather = body.TryGetProperty("weather", out var w) ? w.ToString() : "";
        return response.StatusCode switch
        {
            HttpStatusCode.OK => $"GO : la sonde {probeName} est lancée. Météo : {weather}",
            HttpStatusCode.Conflict => $"NO-GO : lancement de {probeName} refusé. Météo : {weather}",
            _ => $"Réponse inattendue du contrôle de lancement : {(int)response.StatusCode}."
        };
    }

    [McpServerTool]
    [Description("Envoie une commande à une sonde en vol : thrust, rotate, capture-photo ou self-destruct.")]
    public async Task<string> SendCommand(
        [Description("Nom de la sonde")] string probeName,
        [Description("Type de commande : thrust, rotate, capture-photo ou self-destruct")] string type,
        [Description("Valeur : deltaV pour thrust, degrés pour rotate, ignorée sinon")] double? value = null,
        CancellationToken ct = default)
    {
        if (!CommandTypes.Contains(type))
            return $"Type de commande inconnu : {type}. Types valides : {string.Join(", ", CommandTypes)}.";

        var command = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["id"] = Guid.NewGuid(),
            ["probeName"] = probeName
        };
        if (type == "thrust") command["deltaV"] = value ?? 0;
        if (type == "rotate") command["degrees"] = value ?? 0;

        using var response = await httpClientFactory.CreateClient("missioncontrol")
            .PostAsJsonAsync("/commands", command, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (body.TryGetProperty("reason", out var reason))
            return $"Commande rejetée : {reason.GetString()}";
        if (body.TryGetProperty("after", out _))
            return "Délai dépassé : la sonde n'a pas répondu à temps.";
        if (body.TryGetProperty("probeName", out var lost))
            return $"Contact perdu avec la sonde {lost.GetString()}.";
        return "Commande acquittée.";
    }

    [McpServerTool]
    [Description("Donne la dernière télémétrie reçue d'une sonde.")]
    public async Task<string> GetTelemetry(
        [Description("Nom de la sonde")] string probeName,
        CancellationToken ct = default)
    {
        using var response = await httpClientFactory.CreateClient("telemetryreceiver")
            .GetAsync($"/telemetry/{Uri.EscapeDataString(probeName)}/latest", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return $"Aucune télémétrie reçue pour la sonde {probeName}.";
        response.EnsureSuccessStatusCode();

        var frame = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var inv = CultureInfo.InvariantCulture;
        return $"Sonde {probeName} : altitude {frame.GetProperty("altitudeKm").GetDouble().ToString("0.##", inv)} km, "
            + $"vitesse {frame.GetProperty("velocityKmS").GetDouble().ToString("0.##", inv)} km/s, "
            + $"batterie {frame.GetProperty("batteryPercent").GetDouble().ToString("0.##", inv)} %.";
    }
}
