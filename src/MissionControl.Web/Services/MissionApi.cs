using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MissionControl.Web.Services;

/// <summary>Un échange HTTP tel qu'on le montre au public : requête, réponse, durée.</summary>
public sealed record Exchange(
    string Service, string Method, string Path,
    IReadOnlyList<KeyValuePair<string, string>> RequestHeaders, string? RequestBody,
    int? Status, string? Reason, string? ContentType, string? ResponseBody,
    TimeSpan Elapsed, string? Error)
{
    /// <summary>Trace OpenTelemetry de l'appel, pour l'ouvrir dans le dashboard Aspire.</summary>
    public string? TraceId { get; init; }

    public bool IsSuccess => Status is >= 200 and < 300;
    public string Url => $"http://{Service}{Path}";

    /// <summary>Corps de réponse en JSON (première valeur), ou null.</summary>
    public JsonNode? Json
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ResponseBody)) return null;
            try { return JsonNode.Parse(ResponseBody.Split("\n\n")[0]); }
            catch (JsonException) { return null; }
        }
    }
}

/// <summary>Appels aux services de la mission, via les clients nommés et la découverte de services.</summary>
public sealed class MissionApi(IHttpClientFactory clients)
{
    public const string ProbeFactory = "probefactory";
    public const string LaunchControl = "launchcontrol";
    public const string ProbeSimulator = "probesimulator";
    public const string TelemetryReceiver = "telemetryreceiver";
    public const string MissionControl = "missioncontrol";
    public const string Mcp = "mcp";

    // Un span racine par appel de la console : la trace porte un nom lisible et on connaît son identifiant.
    public const string ActivitySourceName = "MissionControl.Console";
    private static readonly ActivitySource Source = new(ActivitySourceName);

    public static readonly string[] Services =
        [ProbeFactory, LaunchControl, ProbeSimulator, TelemetryReceiver, MissionControl, Mcp];

    // Affichage seulement : on garde « é » et « + » lisibles au lieu des échappements é.
    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public HttpClient Client(string service) => clients.CreateClient(service);

    public Task<Exchange> GetAsync(string service, string path, CancellationToken ct = default) =>
        SendAsync(service, HttpMethod.Get, path, null, [], ct);

    public Task<Exchange> PostAsync(string service, string path, object body,
        IEnumerable<KeyValuePair<string, string>>? headers = null, CancellationToken ct = default) =>
        SendAsync(service, HttpMethod.Post, path, JsonSerializer.Serialize(body, Pretty), headers ?? [], ct);

    public async Task<Exchange> SendAsync(string service, HttpMethod method, string path, string? json,
        IEnumerable<KeyValuePair<string, string>> headers, CancellationToken ct = default)
    {
        var shown = new List<KeyValuePair<string, string>>();
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            shown.Add(new("Content-Type", "application/json"));
        }
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
            shown.Add(new(name, value));
        }

        using var activity = Source.StartActivity($"console {method.Method} {path}");
        var traceId = activity?.TraceId.ToHexString();
        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await Client(service).SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            return new Exchange(service, method.Method, path, shown, json, (int)response.StatusCode,
                response.ReasonPhrase, contentType, Format(body, contentType), watch.Elapsed, null) { TraceId = traceId };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new Exchange(service, method.Method, path, shown, json, null, null, null, null,
                watch.Elapsed, $"{service} injoignable : {e.Message}") { TraceId = traceId };
        }
    }

    /// <summary>Lit une réponse JSONL ligne par ligne, au fil de l'arrivée des octets.</summary>
    public async IAsyncEnumerable<string> StreamLinesAsync(string service, string path,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await Client(service).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
            if (line.Length > 0) yield return line;
    }

    /// <summary>Appel JSON-RPC au serveur MCP (transport HTTP sans état ; la réponse peut arriver en SSE).</summary>
    public Task<Exchange> McpAsync(int id, string method, object? parameters, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
        if (parameters is not null) payload["params"] = parameters;
        return SendAsync(Mcp, HttpMethod.Post, "/", JsonSerializer.Serialize(payload, Pretty),
            [new("Accept", "application/json, text/event-stream")], ct);
    }

    private static string Format(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;
        if (contentType == "text/event-stream")
        {
            // Réponse SSE : on ne garde que les données JSON des événements.
            var data = body.Split('\n')
                .Where(l => l.StartsWith("data:", StringComparison.Ordinal))
                .Select(l => Format(l[5..].Trim(), "application/json"));
            return string.Join("\n\n", data);
        }
        if (contentType is null || !contentType.Contains("json") || contentType.Contains("jsonl")) return body;
        try { return JsonNode.Parse(body)?.ToJsonString(Pretty) ?? body; }
        catch (JsonException) { return body; }
    }
}
