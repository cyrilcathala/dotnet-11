# Mission Control .NET 11 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construire la suite de démos « Mission Control » qui montre les nouveautés .NET 11, chaque nouveauté étant accompagnée de son équivalent .NET 10.

**Architecture:** Un AppHost Aspire 17 orchestre six petits services ASP.NET Core (ProbeFactory, LaunchControl, ProbeSimulator, TelemetryReceiver, MissionControl.Api, MissionControl.Mcp). La télémétrie circule en CBOR compressé en Zstd. Deux outils de bord sont des file-based apps (`tools/`). Un projet `compare/net10` contient les versions « avant » en `net10.0`, et un projet `benchmarks/` mesure Zstd et les streams sans copie.

**Tech Stack:** .NET 11 SDK (C# 15), Aspire 17.0.0, ASP.NET Core 11 Minimal APIs, System.Formats.Cbor, System.IO.Compression (Zstandard), OpenTelemetry (exporteur OTLP uniquement), MCP C# SDK v2.0 via `dotnet new mcpserver`, xUnit v3, Microsoft.AspNetCore.Mvc.Testing, Aspire.Hosting.Testing, BenchmarkDotNet.

**Spec:** `docs/superpowers/specs/2026-11-22-mission-control-dotnet11-design.md`

## Global Constraints

- Toutes les applis ciblent `net11.0` ; C# 15 est la version par défaut (pas de `LangVersion` explicite, sauf si la Task 1 montre qu'unions/`closed` exigent `preview`).
- `compare/net10` cible `net10.0` et doit compiler.
- Aucune dépendance réseau externe à l'exécution (météo simulée, pas d'API tierce).
- Aucun package ajouté uniquement pour le comparatif : pas de OneOf, pas de `Results<…>` pour illustrer les unions, pas de `OpenTelemetry.Instrumentation.AspNetCore` côté .NET 11.
- Aucun `#if NET11_0_OR_GREATER`.
- Stockage en mémoire partout.
- Noms de ressources Aspire : `probefactory`, `launchcontrol`, `probesimulator`, `telemetryreceiver`, `missioncontrol`, `mcp`, `asteroid-scan`.
- Les textes affichés au public sont en français ; les messages de validation existent en `fr` et `en`, `en` étant la culture par défaut.

## Review Focus

1. **Culture non supportée** (`Accept-Language: de`) : les messages de validation retombent en anglais, pas de clé brute ni d'exception. → test dans la Task 3.
2. **Flux CBOR tronqué** (la connexion tombe au milieu d'une trame) : les trames complètes déjà décodées sont conservées, la trame partielle est ignorée, la réponse n'est pas une 500. → test dans la Task 7.
3. **Commande vers une sonde inconnue ou encore au sol** : résultat `Rejected` avec une raison lisible, jamais une 500. → test dans la Task 8.
4. **Paquets dupliqués arrivant dans le désordre** : comptés une seule fois, compteur de doublons incrémenté. → test dans la Task 7.
5. **`preflight.cs` en échec technique** (code de sortie autre que 0 ou 1, ex. erreur de compilation) : réponse 503 « Préflight indisponible », jamais un GO. → test dans la Task 4.

---

## File Structure

```
global.json                          SDK 11.0.100
Directory.Build.props                net11.0, Nullable, ImplicitUsings
MissionControl.slnx
src/
  MissionControl.ServiceDefaults/    Extensions.cs : OTel natif, service discovery, health
  MissionControl.Contracts/          TelemetryFrame, TelemetryCbor, DTO partagés
  ProbeFactory/                      Program.cs, CreateProbeRequest.cs, UniqueProbeNameAttribute.cs,
                                     ProbeRegistry.cs, Resources/ValidationMessages(.fr).resx
  LaunchControl/                     Program.cs, PreflightRunner.cs, CountdownSequence.cs
  ProbeSimulator/                    Program.cs, FlightRegistry.cs, TelemetryEmitter.cs
  TelemetryReceiver/                 Program.cs, IncrementalTelemetryDecoder.cs, FlightRecorder.cs, TelemetryStore.cs
  MissionControl.Api/                Program.cs, ProbeCommand.cs, CommandResult.cs, CommandDispatcher.cs, CommandHistory.cs
  MissionControl.Mcp/                (généré par dotnet new mcpserver) Tools/MissionTools.cs
  MissionControl.AppHost/            AppHost.cs
tools/
  preflight.cs
  asteroid-scan.cs, SectorMap.cs, Grid.cs
compare/net10/Compare.Net10/         une paire « avant » par démo, console net10.0
benchmarks/MissionControl.Benchmarks/
tests/
  Contracts.Tests/  ProbeFactory.Tests/  LaunchControl.Tests/  ProbeSimulator.Tests/
  TelemetryReceiver.Tests/  MissionControl.Api.Tests/  Mcp.Tests/  Tools.Tests/  AppHost.Tests/
docs/demo-runbook.md
```

Un projet de tests par service : chaque service a son propre `Program` top-level, qu'un seul projet de tests ne pourrait pas référencer sans ambiguïté. Chaque `Program.cs` se termine par `public partial class Program;`.

## Contrats partagés (référence pour toutes les tâches)

```csharp
namespace MissionControl.Contracts;

public enum ProbeKind { Orbiter, Lander, Rover }
public sealed record ProbeDto(string Name, ProbeKind Kind, DateTimeOffset CreatedAt);
public sealed record LaunchRequest(string ProbeName);
public sealed record LaunchReport(string ProbeName, string Status, string Weather); // Status: "GO" | "NO-GO"
public sealed record StartFlightRequest(string ProbeName);
public sealed record SimulatorCommandRequest(Guid CommandId, string Type, double? Value); // Type: "thrust" | "rotate" | "capture-photo" | "self-destruct"
public sealed record SimulatorCommandOutcome(string Outcome, string? Reason);              // Outcome: "ack" | "rejected" | "lost"
public sealed record TelemetryFrame(long PacketId, string ProbeName, DateTimeOffset Timestamp,
                                    double AltitudeKm, double VelocityKmS, double BatteryPercent);
public sealed record TelemetryStats(long Received, long Duplicates);
```

| Service | Endpoint | Réponse |
|---|---|---|
| ProbeFactory | `POST /probes` (`CreateProbeRequest`) | 201 `ProbeDto` / 400 `ValidationProblem` |
| ProbeFactory | `GET /probes/{name}` | 200 `ProbeDto` / 404 |
| LaunchControl | `POST /launches?forceStorm=bool` (`LaunchRequest`) | 200 GO / 409 NO-GO / 404 sonde inconnue / 503 préflight indisponible |
| LaunchControl | `GET /diagnostics/countdown-stack` | 200 `{ frames: int, stack: string }` |
| ProbeSimulator | `POST /flights` (`StartFlightRequest`) | 202 |
| ProbeSimulator | `POST /flights/{probe}/commands` (`SimulatorCommandRequest`) | 200 `SimulatorCommandOutcome` / 404 vol inconnu |
| TelemetryReceiver | `POST /telemetry/{probe}` (CBOR, `Content-Encoding: zstd`, streaming) | 202 |
| TelemetryReceiver | `GET /telemetry/{probe}/latest` | 200 `TelemetryFrame` / 404 |
| TelemetryReceiver | `GET /telemetry/stats` | 200 `TelemetryStats` |
| MissionControl.Api | `POST /commands` (`ProbeCommand` polymorphe) | 200 `CommandResult` |
| MissionControl.Api | `GET /commands/history` | 200 `application/jsonl` |

---

### Task 1: Vérifier les API incertaines (spike jetable)

But : lever les six « points à vérifier » de la spec (section 7) **avant** de construire, sur le SDK 11 final.

**Files:**
- Create: `spikes/` (jetable, supprimé à la fin de la tâche)
- Modify: `docs/superpowers/specs/2026-11-22-mission-control-dotnet11-design.md` (section 7 : résultat de chaque point)

- [ ] **Step 1: Installer le SDK avec dotnetup** et noter les commandes exactes utilisées (installation, mise à jour, liste). Run: `dotnet --version` — Expected: `11.0.1xx`.
- [ ] **Step 2: Unions et `closed`** — un `spikes/unions.cs` avec `public union CommandResult(A, B);` et un `closed record`. Run: `dotnet run spikes/unions.cs` — noter s'il faut `#:property LangVersion=preview`.
- [ ] **Step 3: `dotnet run -e`** — un `spikes/env.cs` qui affiche `Environment.GetEnvironmentVariable("X")`. Trouver la forme qui marche avec un fichier : `dotnet run -e X=1 spikes/env.cs` ou `dotnet run -e X=1 --file spikes/env.cs`. Expected: `1`.
- [ ] **Step 4: CBOR incrémental** — vérifier dans la doc d'API la signature exacte de `CborReader.SlideData` et s'il faut `AllowMultipleRootLevelValues` dans `CborReaderOptions` pour lire une suite de trames. Petit spike : écrire 3 trames, les relire en morceaux de 7 octets.
- [ ] **Step 5: Zstd côté HTTP** — vérifier si `AddRequestDecompression()` gère `Content-Encoding: zstd` par défaut en ASP.NET Core 11, ou s'il faut enregistrer un provider (`ZstandardCompressionProviderOptions`…).
- [ ] **Step 6: Validation** — vérifier `AsyncValidationAttribute.IsValidAsync(object?, ValidationContext, CancellationToken)` et le nom de l'option `LocalizerProvider` dans `AddValidation(...)`.
- [ ] **Step 7: OpenTelemetry en .NET 10 et certificats WSL** — confirmer ce qu'exigeait .NET 10 (package `OpenTelemetry.Instrumentation.AspNetCore`) et chercher l'auto-trust WSL dans les notes de version du SDK. Si introuvable, l'écrire « retiré du périmètre ».
- [ ] **Step 8: Reporter les résultats** dans la section 7 de la spec (une ligne par point : confirmé / ajusté / retiré), supprimer `spikes/`, puis informer l'humain des écarts avant de continuer.

---

### Task 2: Squelette de solution, ServiceDefaults et contrats

**Files:**
- Create: `global.json`, `Directory.Build.props`, `MissionControl.slnx`, `.gitignore`
- Create: `src/MissionControl.ServiceDefaults/Extensions.cs`
- Create: `src/MissionControl.Contracts/Contracts.cs`, `src/MissionControl.Contracts/TelemetryCbor.cs`
- Test: `tests/Contracts.Tests/TelemetryCborTests.cs`

**Interfaces:**
- Produces: les contrats ci-dessus ; `TelemetryCbor.Write(CborWriter writer, TelemetryFrame frame)`, `TelemetryCbor.Read(CborReader reader) -> TelemetryFrame`, `TelemetryCbor.Encode(TelemetryFrame frame) -> byte[]` ; `IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)` et `WebApplication MapDefaultEndpoints(this WebApplication app)`.

- [ ] **Step 1: `git init`**, `global.json` (SDK `11.0.100`, `rollForward: latestFeature`), `Directory.Build.props` (`TargetFramework` net11.0, `Nullable` enable, `ImplicitUsings` enable). `compare/` et `benchmarks/` surchargent `TargetFramework` dans leur propre `.csproj`.
- [ ] **Step 2: Write the failing tests**

```csharp
[Fact] public void Encode_then_Read_round_trips_all_fields()
{
    var frame = new TelemetryFrame(42, "Voyager-3", DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000), 410.5, 7.66, 88.0);
    var reader = new CborReader(TelemetryCbor.Encode(frame));
    Assert.Equal(frame, TelemetryCbor.Read(reader));
}

[Fact] public void Encode_writes_a_six_entry_map()
    => Assert.Equal(0xA6, TelemetryCbor.Encode(SampleFrame())[0]);
```

- [ ] **Step 3: Run** `dotnet test tests/Contracts.Tests` — Expected: FAIL (types absents).
- [ ] **Step 4: Implémenter** les contrats et `TelemetryCbor` : une map CBOR à 6 clés texte `id`, `probe`, `ts` (millisecondes Unix, int64), `alt`, `vel`, `bat`, dans cet ordre. `Read` accepte les clés dans n'importe quel ordre.
- [ ] **Step 5: Implémenter `AddServiceDefaults`** : service discovery, health checks, et OpenTelemetry avec `.WithTracing(t => t.AddSource("Microsoft.AspNetCore").AddSource("System.Net.Http"))` + exporteur OTLP. **Aucun** package `OpenTelemetry.Instrumentation.*` (c'est la démo).
- [ ] **Step 6: Run** `dotnet test tests/Contracts.Tests` — Expected: PASS.
- [ ] **Step 7: Commit** `chore: squelette de solution, ServiceDefaults et contrats`.

---

### Task 3: ProbeFactory — validation async, localisation, arguments de collection

**Files:**
- Create: `src/ProbeFactory/Program.cs`, `CreateProbeRequest.cs`, `UniqueProbeNameAttribute.cs`, `ProbeRegistry.cs`, `ValidationMessages.cs`, `Resources/ValidationMessages.resx`, `Resources/ValidationMessages.fr.resx`
- Test: `tests/ProbeFactory.Tests/CreateProbeTests.cs`

**Interfaces:**
- Consumes: `ProbeKind`, `ProbeDto` (Task 2), `AddServiceDefaults`.
- Produces: `POST /probes`, `GET /probes/{name}`.

`CreateProbeRequest(string Name, ProbeKind Kind)` porte `[Required]`, `[StringLength(32, MinimumLength = 3)]` et `[UniqueProbeName]` sur `Name`.

`IProbeRegistry` : `ValueTask<bool> IsTakenAsync(string name, CancellationToken ct)`, `ProbeDto Add(string name, ProbeKind kind)`, `ProbeDto? Find(string name)`. `InMemoryProbeRegistry` contient la démo des arguments de collection :

```csharp
private readonly HashSet<string> _reserved = [with(StringComparer.OrdinalIgnoreCase), "Voyager", "Apollo"];
```

`IsTakenAsync` attend 300 ms (registre « distant ») puis renvoie vrai si le nom est réservé ou déjà enregistré, sans tenir compte de la casse.

Messages (fichier partagé `ValidationMessages`, branché via `LocalizerProvider` dans `AddValidation`) — copie exacte :

| Clé | en | fr |
|---|---|---|
| `RequiredAttribute_Error` | `The {0} field is required.` | `Le champ {0} est obligatoire.` |
| `StringLengthAttribute_Error` | `The {0} field must be between {2} and {1} characters.` | `Le champ {0} doit contenir entre {2} et {1} caractères.` |
| `UniqueProbeNameAttribute_Error` | `This probe name is already taken.` | `Ce nom de sonde est déjà pris.` |

`UseRequestLocalization` : cultures supportées `en`, `fr` ; défaut `en`.

- [ ] **Step 1: Write the failing tests** (WebApplicationFactory)

```csharp
[Fact] public async Task Creates_a_probe() { /* POST {Name:"Pioneer-11", Kind:Orbiter} */ Assert.Equal(HttpStatusCode.Created, resp.StatusCode); }
[Fact] public async Task Rejects_a_reserved_name_case_insensitively() { /* Name:"voyager" */ AssertError(body, "Name", "This probe name is already taken."); }
[Fact] public async Task Rejects_a_duplicate_name() { /* deux POST "Pioneer-12" */ Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode); }
[Fact] public async Task Validation_messages_are_in_french_with_accept_language_fr() { /* Name:"" + Accept-Language: fr */ AssertError(body, "Name", "Le champ Name est obligatoire."); }
[Fact] public async Task Unsupported_culture_falls_back_to_english() { /* Accept-Language: de */ AssertError(body, "Name", "The Name field is required."); }   // Review Focus 1
[Fact] public async Task Get_returns_404_for_unknown_probe() { ... }
```

- [ ] **Step 2: Run** `dotnet test tests/ProbeFactory.Tests` — Expected: FAIL.
- [ ] **Step 3: Implémenter** registre, attribut async (récupère `IProbeRegistry` via `validationContext.GetService`), endpoints, `AddLocalization()` + `AddValidation(...)`.
- [ ] **Step 4: Run** — Expected: PASS.
- [ ] **Step 5: Commit** `feat(probefactory): validation async localisée`.

---

### Task 4: `preflight.cs` et le lancement dans LaunchControl

**Files:**
- Create: `tools/preflight.cs`
- Create: `src/LaunchControl/Program.cs`, `PreflightRunner.cs`
- Test: `tests/LaunchControl.Tests/PreflightRunnerTests.cs`, `tests/LaunchControl.Tests/LaunchEndpointTests.cs`

**Interfaces:**
- Consumes: `LaunchRequest`, `LaunchReport`, `ProbeDto`, `StartFlightRequest` (Task 2) ; `GET /probes/{name}` (Task 3) ; `POST /flights` (Task 6, via HttpClient nommé `probesimulator`).
- Produces: `IPreflightRunner.RunAsync(string probeName, bool forceStorm, CancellationToken ct) -> Task<PreflightResult>` avec `record PreflightResult(PreflightVerdict Verdict, string Report)` et `enum PreflightVerdict { Go, NoGo, Unavailable }` ; `POST /launches`.

`tools/preflight.cs` lit `LAUNCH_SITE`, `PROBE`, `FORCE_STORM`. Sortie (copie exacte, une ligne chacune) :

```
Station météo — {LAUNCH_SITE} — sonde {PROBE}
Vent : 12 km/h
Orages : aucun                      | Orages : cellule orageuse à 8 km
Couverture nuageuse : 20 %          | Couverture nuageuse : 90 %
GO                                  | NO-GO
```

Code de sortie 0 si GO, 1 si NO-GO. Déterministe : seul `FORCE_STORM=true` provoque le NO-GO.

`PreflightRunner` contient la démo `Process` (chemin de l'outil lu dans la config `Preflight:ToolPath`) ; utiliser la forme `-e` confirmée à la Task 1 :

```csharp
var result = await Process.RunAndCaptureTextAsync("dotnet",
    ["run", "-e", "LAUNCH_SITE=Kourou", "-e", $"PROBE={probeName}", "-e", $"FORCE_STORM={forceStorm}", "--file", toolPath]);
```

Correspondance : code de sortie 0 → `Go`, 1 → `NoGo`, autre → `Unavailable`.

`POST /launches` : sonde inconnue chez ProbeFactory → 404 ; `Go` → appelle `POST /flights` puis 200 `LaunchReport(name, "GO", report)` ; `NoGo` → 409 `LaunchReport(name, "NO-GO", report)` ; `Unavailable` → 503 avec `detail: "Préflight indisponible"`.

- [ ] **Step 1: Write the failing tests**

```csharp
// PreflightRunnerTests (intégration réelle avec tools/preflight.cs)
[Fact] public async Task Clear_weather_is_go() { var r = await Runner().RunAsync("Pioneer-11", false, ct); Assert.Equal(PreflightVerdict.Go, r.Verdict); Assert.Contains("Orages : aucun", r.Report); }
[Fact] public async Task Forced_storm_is_no_go() { ...; Assert.Equal(PreflightVerdict.NoGo, r.Verdict); }
[Fact] public async Task Broken_tool_is_unavailable() { /* ToolPath vers un .cs qui ne compile pas */ Assert.Equal(PreflightVerdict.Unavailable, r.Verdict); }   // Review Focus 5

// LaunchEndpointTests (IPreflightRunner et HttpClients remplacés par des fakes)
[Fact] public async Task Go_starts_the_flight_and_returns_200() { ...; Assert.Single(fakeSimulator.StartedFlights, "Pioneer-11"); }
[Fact] public async Task No_go_returns_409_and_starts_nothing() { ... }
[Fact] public async Task Unavailable_preflight_returns_503_never_go() { ... }   // Review Focus 5
[Fact] public async Task Unknown_probe_returns_404() { ... }
```

- [ ] **Step 2: Run** `dotnet test tests/LaunchControl.Tests` — Expected: FAIL.
- [ ] **Step 3: Implémenter** `preflight.cs`, `PreflightRunner`, l'endpoint.
- [ ] **Step 4: Run** — Expected: PASS. Vérifier aussi à la main : `dotnet run -e FORCE_STORM=true --file tools/preflight.cs` affiche `NO-GO` et `echo $LASTEXITCODE` donne `1`.
- [ ] **Step 5: Commit** `feat(launchcontrol): préflight météo via l'API Process et dotnet run -e`.

---

### Task 5: Runtime async — pile d'appels du compte à rebours

**Files:**
- Create: `src/LaunchControl/CountdownSequence.cs`
- Modify: `src/LaunchControl/LaunchControl.csproj` (ajouter `<Features>runtime-async=on</Features>`), `src/LaunchControl/Program.cs`
- Test: `tests/LaunchControl.Tests/CountdownStackTests.cs`

**Interfaces:**
- Produces: `CountdownSequence.CaptureStackAsync(CancellationToken ct) -> Task<string>` ; `GET /diagnostics/countdown-stack` → `{ frames, stack }`.

Chaîne d'appels async : `CaptureStackAsync → IgnitionSequenceAsync → StageSeparationAsync → OrbitInsertionAsync`. Chaque méthode fait un `await Task.Yield()` avant d'appeler la suivante ; la dernière renvoie `new StackTrace(fNeedFileInfo: true).ToString()`. Ce même fichier est recopié tel quel dans `compare/net10` (Task 12), pour comparer le nombre de frames.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] public async Task Stack_names_every_countdown_step()
{
    var stack = await new CountdownSequence().CaptureStackAsync(ct);
    Assert.Contains("OrbitInsertionAsync", stack);
    Assert.Contains("StageSeparationAsync", stack);
    Assert.Contains("IgnitionSequenceAsync", stack);
}
[Fact] public async Task Endpoint_returns_frame_count() { ...; Assert.True(body.Frames > 0); }
```

- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implémenter** `CountdownSequence` et l'endpoint (frames = nombre de lignes commençant par `   at `).
- [ ] **Step 4: Run** — Expected: PASS. Noter dans le runbook le nombre de frames obtenu avec et sans `<Features>runtime-async=on</Features>`.
- [ ] **Step 5: Commit** `feat(launchcontrol): pile d'appels live avec runtime async`.

---

### Task 6: ProbeSimulator — vols, télémétrie Zstd, commandes

**Files:**
- Create: `src/ProbeSimulator/Program.cs`, `FlightRegistry.cs`, `TelemetryEmitter.cs`, `TelemetryContent.cs`
- Test: `tests/ProbeSimulator.Tests/FlightTests.cs`, `tests/ProbeSimulator.Tests/TelemetryContentTests.cs`

**Interfaces:**
- Consumes: `StartFlightRequest`, `SimulatorCommandRequest`, `SimulatorCommandOutcome`, `TelemetryFrame`, `TelemetryCbor` (Task 2).
- Produces: `POST /flights`, `POST /flights/{probe}/commands` ; `TelemetryContent(IAsyncEnumerable<TelemetryFrame> frames) : HttpContent`.

- `FlightRegistry` : `bool Start(string probe)`, `SimulatorCommandOutcome Apply(string probe, SimulatorCommandRequest cmd)`. Sonde sans vol → l'endpoint renvoie 404. `self-destruct` → `("lost", null)` et le vol s'arrête. Toute commande vers un vol arrêté → `("lost", "Contact perdu")`. `thrust` avec `Value` ≤ 0 → `("rejected", "Poussée invalide")`. Sinon `("ack", null)`.
- `TelemetryEmitter` (BackgroundService) : pour chaque vol actif, une requête `POST /telemetry/{probe}` longue vers `telemetryreceiver`, dont le corps est un `TelemetryContent`.
- `TelemetryContent` contient la démo Zstd : il écrit dans `new ZstandardStream(stream, CompressionMode.Compress)`, ajoute l'en-tête `Content-Encoding: zstd`, et fait un flush après chaque trame. Une trame toutes les 500 ms ; toutes les 5 trames, la précédente est renvoyée (même `PacketId`) pour simuler une retransmission. Altitude, vitesse et batterie évoluent linéairement à partir de `410`, `7.66`, `100`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Self_destruct_returns_lost_then_every_command_returns_lost() { ... Assert.Equal("lost", second.Outcome); Assert.Equal("Contact perdu", second.Reason); }
[Fact] public async Task Negative_thrust_is_rejected() { ... }
[Fact] public async Task Command_for_unknown_flight_returns_404() { ... }
[Fact] public async Task TelemetryContent_is_zstd_compressed_cbor_with_retransmissions()
{
    // sérialiser 6 trames, décompresser avec ZstandardStream, relire avec CborReader
    Assert.Equal("zstd", content.Headers.ContentEncoding.Single());
    Assert.Equal(new long[] { 1, 2, 3, 4, 5, 5, 6 }, decoded.Select(f => f.PacketId));
}
```

- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implémenter.** L'intervalle de 500 ms est injectable (`TimeProvider`) pour que les tests ne dorment pas.
- [ ] **Step 4: Run** — Expected: PASS.
- [ ] **Step 5: Commit** `feat(simulator): vols, commandes et télémétrie CBOR compressée en Zstd`.

---

### Task 7: TelemetryReceiver — CBOR incrémental, boîte noire, dédoublonnage

**Files:**
- Create: `src/TelemetryReceiver/Program.cs`, `IncrementalTelemetryDecoder.cs`, `FlightRecorder.cs`, `TelemetryStore.cs`
- Test: `tests/TelemetryReceiver.Tests/DecoderTests.cs`, `StoreTests.cs`, `IngestEndpointTests.cs`

**Interfaces:**
- Consumes: `TelemetryFrame`, `TelemetryCbor.Read`, `TelemetryStats` (Task 2).
- Produces: `IncrementalTelemetryDecoder` avec `IReadOnlyList<TelemetryFrame> Push(ReadOnlyMemory<byte> chunk, bool isFinalBlock)` ; `IFlightRecorder.AppendAsync(string probe, Stream raw, CancellationToken ct)` ; `TelemetryStore` avec `bool TryAdd(TelemetryFrame f)`, `TelemetryFrame? Latest(string probe)`, `TelemetryStats Stats`. Endpoints listés plus haut.

- **Décodeur** (démo CBOR incrémental) : un `CborReader` créé avec `ConformanceMode = CborConformanceMode.Lax` et `isFinalBlock: false` ; tant que `PeekState()` ne vaut pas `NeedsMoreData`, il lit une trame avec `TelemetryCbor.Read` ; chaque nouveau morceau est donné à `SlideData(chunk, isFinalBlock)`. Suivre le contrat exact relevé à la Task 1. Une trame incomplète à la fin du flux est abandonnée sans exception.
- **Endpoint d'ingestion** : décompression Zstd de la requête activée comme établi à la Task 1 ; lit `Request.BodyReader` ; pour chaque `ReadResult`, il archive le buffer brut avec `recorder.AppendAsync(probe, new ReadOnlySequenceStream(result.Buffer), ct)` (démo du stream sans copie), puis donne chaque segment au décodeur et ajoute les trames au store. Une connexion coupée en plein milieu termine la lecture proprement.
- **Store** (démo comparer) :

```csharp
private readonly HashSet<TelemetryFrame> _seen = new(EqualityComparer<TelemetryFrame>.Create(f => f.PacketId));
```

  La clé est par sonde : un `HashSet` par `ProbeName`. Les doublons incrémentent `Stats.Duplicates`.
- `InMemoryFlightRecorder` conserve les octets reçus par sonde (`byte[] Snapshot(string probe)`).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Decodes_frames_split_into_7_byte_chunks() { /* 3 trames encodées, découpées en morceaux de 7 octets */ Assert.Equal(new long[] { 1, 2, 3 }, frames.Select(f => f.PacketId)); }
[Fact] public void Drops_a_truncated_last_frame_without_throwing() { /* 2 trames + moitié d'une 3e, isFinalBlock: true */ Assert.Equal(2, frames.Count); }   // Review Focus 2
[Fact] public void Duplicates_out_of_order_are_counted_once() { /* ids 1,3,2,3,1 */ Assert.Equal(new TelemetryStats(3, 2), store.Stats); }   // Review Focus 4
[Fact] public async Task Ingest_accepts_zstd_cbor_and_exposes_latest() { /* POST du TelemetryContent de la Task 6 */ Assert.Equal(6, latest.PacketId); }
[Fact] public async Task Ingest_records_raw_bytes_in_flight_recorder() { Assert.NotEmpty(recorder.Snapshot("Pioneer-11")); }
[Fact] public async Task Aborted_upload_keeps_decoded_frames_and_does_not_500() { ... }   // Review Focus 2
```

- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implémenter.**
- [ ] **Step 4: Run** — Expected: PASS.
- [ ] **Step 5: Commit** `feat(receiver): décodage CBOR incrémental, boîte noire et dédoublonnage`.

---

### Task 8: MissionControl.Api — `closed`, union, OpenTelemetry, historique JSONL

**Files:**
- Create: `src/MissionControl.Api/Program.cs`, `ProbeCommand.cs`, `CommandResult.cs`, `CommandDispatcher.cs`, `CommandHistory.cs`
- Test: `tests/MissionControl.Api.Tests/DispatcherTests.cs`, `CommandsEndpointTests.cs`, `HistoryEndpointTests.cs`

**Interfaces:**
- Consumes: `SimulatorCommandRequest`, `SimulatorCommandOutcome` (Task 2) ; `POST /flights/{probe}/commands` (Task 6, HttpClient nommé `probesimulator`).
- Produces: `POST /commands`, `GET /commands/history` ; les types ci-dessous (le MCP de la Task 9 n'utilise que leur JSON).

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Thrust), "thrust")]          // + "rotate", "capture-photo", "self-destruct"
public closed record ProbeCommand(Guid Id, string ProbeName);
public record Thrust(Guid Id, string ProbeName, double DeltaV) : ProbeCommand(Id, ProbeName);
public record Rotate(Guid Id, string ProbeName, double Degrees) : ProbeCommand(Id, ProbeName);
public record CapturePhoto(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);
public record SelfDestruct(Guid Id, string ProbeName) : ProbeCommand(Id, ProbeName);

public record Acknowledged(Guid CommandId, DateTimeOffset At);
public record Rejected(Guid CommandId, string Reason);
public record TimedOut(Guid CommandId, TimeSpan After);
public record ProbeLost(Guid CommandId, string ProbeName);
public union CommandResult(Acknowledged, Rejected, TimedOut, ProbeLost);

public sealed record CommandHistoryEntry(Guid CommandId, string ProbeName, string Command, string Outcome, DateTimeOffset At);
```

Si la Task 1 a montré que `InferClosedTypePolymorphism` fonctionne, remplacer les `[JsonDerivedType]` par `[JsonPolymorphic(InferClosedTypePolymorphism = true)]`. Le discriminateur `"type"` reste le même.

- **`CommandDispatcher.DispatchAsync(ProbeCommand cmd, CancellationToken ct) -> Task<CommandResult>`** :
  - un `switch` **sans `default`** traduit la commande en `SimulatorCommandRequest` (`thrust`/`DeltaV`, `rotate`/`Degrees`, `capture-photo`, `self-destruct`) ;
  - délai de 2 s → `TimedOut(id, 2 s)` ;
  - 404 du simulateur → `Rejected(id, "Sonde inconnue ou au sol")` ;
  - `"rejected"` → `Rejected(id, reason)` ; `"lost"` → `ProbeLost(id, probe)` ; `"ack"` → `Acknowledged(id, now)`.
  - Les résultats sont créés par conversion implicite (`return new Acknowledged(...)`), sans encapsulation.
- **`static string Describe(CommandResult r)`** : `switch` exhaustif sans `default`, qui renvoie `"ack"`, `"rejected"`, `"timeout"` ou `"lost"`. Sert pour l'historique.
- **`POST /commands`** renvoie toujours 200 avec l'union sérialisée (choix pour que l'OpenAPI montre `anyOf`) et ajoute une entrée à l'historique.
- **`ICommandHistory`** : `void Append(CommandHistoryEntry e)` et `IAsyncEnumerable<CommandHistoryEntry> ReadAllAsync(CancellationToken ct)`. Implémentation en mémoire.
- **`GET /commands/history`** : `Content-Type: application/jsonl`, puis `await JsonSerializer.SerializeAsyncEnumerable(Response.Body, history.ReadAllAsync(ct), topLevelValues: true, cancellationToken: ct)` (signature exacte selon la Task 1).
- `AddOpenApi()` + `MapOpenApi()` (OpenAPI 3.2 par défaut).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Ack_from_simulator_is_Acknowledged() { Assert.IsType<Acknowledged>(result.Value); }   // accès au cas selon l'API d'union confirmée en Task 1
[Fact] public async Task Self_destruct_yields_ProbeLost() { ... }
[Fact] public async Task Unknown_probe_yields_Rejected_not_500() { Assert.Equal("Sonde inconnue ou au sol", rejected.Reason); }   // Review Focus 3
[Fact] public async Task Slow_simulator_yields_TimedOut() { ... }
[Fact] public async Task Post_commands_deserializes_type_discriminator() { /* {"type":"rotate","id":..., "probeName":"Pioneer-11","degrees":90} */ }
[Fact] public async Task OpenApi_describes_CommandResult_with_anyOf() { Assert.Contains("anyOf", openApiJson); }
[Fact] public async Task History_is_json_lines()
{
    // 3 commandes envoyées
    Assert.Equal("application/jsonl", resp.Content.Headers.ContentType!.MediaType);
    var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(3, lines.Length);
    Assert.All(lines, l => JsonDocument.Parse(l));
}
```

- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implémenter.**
- [ ] **Step 4: Run** — Expected: PASS. Vérifier aussi qu'ajouter un `record Refuel(...) : ProbeCommand(...)` provoque un avertissement d'exhaustivité au build, puis le retirer.
- [ ] **Step 5: Commit** `feat(missioncontrol): commandes closed, résultats en union, historique JSONL`.

---

### Task 9: MissionControl.Mcp — l'IA pilote les sondes

**Files:**
- Create: `src/MissionControl.Mcp/` via `dotnet new mcpserver -o src/MissionControl.Mcp` (transport HTTP si le template le propose, pour être hébergé par Aspire)
- Create: `src/MissionControl.Mcp/Tools/MissionTools.cs`
- Test: `tests/Mcp.Tests/MissionToolsTests.cs`

**Interfaces:**
- Consumes: `POST /launches` (Task 4), `POST /commands` (Task 8), `GET /telemetry/{probe}/latest` (Task 7), via les HttpClients nommés `launchcontrol`, `missioncontrol`, `telemetryreceiver`.
- Produces: les outils MCP `LaunchProbe(string probeName)`, `SendCommand(string probeName, string type, double? value)`, `GetTelemetry(string probeName)`. Chacun renvoie une phrase en français lisible par l'agent.

Descriptions des outils (copie exacte) :
- `LaunchProbe` : « Lance une sonde déjà construite, après vérification météo. »
- `SendCommand` : « Envoie une commande à une sonde en vol : thrust, rotate, capture-photo ou self-destruct. »
- `GetTelemetry` : « Donne la dernière télémétrie reçue d'une sonde. »

- [ ] **Step 1: Write the failing tests** (HttpMessageHandler factice)

```csharp
[Fact] public async Task SendCommand_posts_the_type_discriminator() { Assert.Equal("rotate", sentJson.GetProperty("type").GetString()); }
[Fact] public async Task LaunchProbe_reports_no_go() { Assert.Contains("NO-GO", answer); }
[Fact] public async Task GetTelemetry_for_silent_probe_says_so() { /* 404 */ Assert.Contains("Aucune télémétrie", answer); }
```

- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implémenter** `MissionTools` et supprimer les outils d'exemple du template.
- [ ] **Step 4: Run** — Expected: PASS.
- [ ] **Step 5: Commit** `feat(mcp): outils de pilotage des sondes`.

---

### Task 10: `asteroid-scan.cs` — outil de bord

**Files:**
- Create: `tools/asteroid-scan.cs`, `tools/SectorMap.cs`, `tools/Grid.cs`
- Test: `tests/Tools.Tests/AsteroidScanTests.cs`

**Interfaces:**
- Produces: `record struct Position(int Row, int Col)` ; `static Task<IReadOnlyList<string>> Grid.LoadAsync(Stream source)` ; un indexeur d'extension dans `Grid.cs` :

```csharp
public static class GridExtensions
{
    extension(IReadOnlyList<string> rows)
    {
        public char this[Position p] => rows[p.Row][p.Col];
    }
}
```

`SectorMap.Kuiper` est la chaîne brute de la spec (3 lignes). `asteroid-scan.cs` : `#:include SectorMap.cs`, `#:include Grid.cs`, charge la carte via `new StringStream(SectorMap.Kuiper, Encoding.UTF8)`, parcourt les lignes avec une boucle étiquetée `outer:` et fait `break outer;` sur le premier `#` (lu avec `grid[pos]`). Sortie (copie exacte) : `Astéroïde détecté en (0, 10) — trajectoire corrigée.` ; s'il n'y a aucun astéroïde : `Secteur dégagé.`

- [ ] **Step 1: Write the failing test** (lance `dotnet run --file tools/asteroid-scan.cs` avec l'API `Process`)

```csharp
[Fact] public async Task Scan_reports_first_asteroid()
{
    var r = await Process.RunAndCaptureTextAsync("dotnet", ["run", "--file", ToolPath("asteroid-scan.cs")]);
    Assert.Equal(0, r.ExitStatus.ExitCode);
    Assert.Contains("Astéroïde détecté en (0, 10) — trajectoire corrigée.", r.StandardOutput);
}
```

- [ ] **Step 2: Run** — Expected: FAIL.
- [ ] **Step 3: Implémenter** les trois fichiers.
- [ ] **Step 4: Run** — Expected: PASS.
- [ ] **Step 5: Commit** `feat(tools): asteroid-scan avec #:include, StringStream, break étiqueté`.

---

### Task 11: AppHost Aspire 17 et test de bout en bout

**Files:**
- Create: `src/MissionControl.AppHost/` (à partir du template Aspire 17, **sans** `Aspire.AppHost.Sdk`, déprécié par ASPIRE012), `AppHost.cs`
- Test: `tests/AppHost.Tests/MissionTests.cs`

**Interfaces:**
- Consumes: tous les services (Tasks 3 à 10).

Références : `launchcontrol` → `probefactory`, `probesimulator` ; `probesimulator` → `telemetryreceiver` ; `missioncontrol` → `probesimulator` ; `mcp` → `launchcontrol`, `missioncontrol`, `telemetryreceiver`. `launchcontrol` reçoit `Preflight__ToolPath` pointant vers `tools/preflight.cs`.

```csharp
builder.AddExecutable("asteroid-scan", "dotnet", "../../tools", "run", "--file", "asteroid-scan.cs");
```

Utiliser les API non dépréciées générées par le template Aspire 17 ; le build ne doit afficher aucun avertissement ASPIRE.

- [ ] **Step 1: Write the failing test** (`DistributedApplicationTestingBuilder`)

```csharp
[Fact] public async Task Full_mission()
{
    // POST probefactory /probes {Pioneer-11, Orbiter}           → 201
    // POST launchcontrol /launches?forceStorm=true              → 409 NO-GO
    // POST launchcontrol /launches                              → 200 GO
    // attendre que telemetryreceiver /telemetry/Pioneer-11/latest réponde 200 (timeout 15 s)
    // POST missioncontrol /commands {type:"self-destruct", ...} → corps contenant "probeName"
    // GET missioncontrol /commands/history                      → 1 ligne JSONL
    // GET telemetryreceiver /telemetry/stats                    → Duplicates > 0
}
```

- [ ] **Step 2: Run** `dotnet test tests/AppHost.Tests` — Expected: FAIL.
- [ ] **Step 3: Implémenter** `AppHost.cs`.
- [ ] **Step 4: Run** — Expected: PASS. Puis `aspire run` : les 7 ressources apparaissent dans le dashboard, une trace « POST /commands » traverse `missioncontrol` → `probesimulator`, et `aspire describe --format mermaid` produit un diagramme. Le sauvegarder dans `docs/architecture.mmd`.
- [ ] **Step 5: Commit** `feat(apphost): orchestration Aspire 17 et test de mission complète`.

---

### Task 12: Comparatif .NET 10

**Files:**
- Create: `compare/net10/Compare.Net10/Compare.Net10.csproj` (`net10.0`, console ; seuls packages autorisés : ceux qu'exigeait .NET 10 pour la même fonctionnalité, par ex. `OpenTelemetry.Instrumentation.AspNetCore`)
- Create, un fichier par démo, nommé comme son équivalent .NET 11 :

| Fichier .NET 10 | Équivalent .NET 11 | Contenu « avant » |
|---|---|---|
| `ProbeFactory/ProbeRegistry.cs` | Task 3 | `new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Voyager", "Apollo" }` |
| `ProbeFactory/CreateProbeEndpoint.cs` | Task 3 | Vérification d'unicité manuelle dans l'endpoint, `ErrorMessageResourceType` sur chaque attribut |
| `LaunchControl/PreflightRunner.cs` | Task 4 | `ProcessStartInfo` + redirections + `psi.Environment[...]` + `ReadToEndAsync` + `WaitForExitAsync` |
| `LaunchControl/CountdownSequence.cs` | Task 5 | Copie identique du fichier .NET 11 |
| `ProbeSimulator/TelemetryContent.cs` | Task 6 | `BrotliStream` à la place de `ZstandardStream` |
| `TelemetryReceiver/BufferedTelemetryDecoder.cs` | Task 7 | Tout le corps mis en tampon, puis décodage d'un bloc |
| `TelemetryReceiver/FlightRecorder.cs` | Task 7 | `new MemoryStream(buffer.ToArray())` |
| `TelemetryReceiver/PacketIdComparer.cs` | Task 7 | Classe `IEqualityComparer<TelemetryFrame>` écrite à la main |
| `MissionControl/ProbeCommand.cs` | Task 8 | `abstract record` + `switch` avec `default: throw` |
| `MissionControl/CommandResult.cs` | Task 8 | `abstract record CommandResult` dont chaque cas hérite |
| `MissionControl/CommandHistoryEndpoint.cs` | Task 8 | Boucle `await foreach` qui sérialise puis écrit `\n` |
| `ServiceDefaults/Extensions.cs` | Task 2 | `AddAspNetCoreInstrumentation()` + package d'instrumentation |

- [ ] **Step 1: Créer le projet** et chaque fichier ; `Program.cs` expose `dotnet run -- countdown`, qui affiche `Frames : {n}` puis la pile.
- [ ] **Step 2: Run** `dotnet build compare/net10` — Expected: build réussi, 0 erreur.
- [ ] **Step 3: Run** `dotnet run --project compare/net10/Compare.Net10 -- countdown` — noter `n` dans le runbook à côté du résultat .NET 11 de la Task 5.
- [ ] **Step 4: Commit** `feat(compare): versions .NET 10 des démos`.

---

### Task 13: Benchmarks

**Files:**
- Create: `benchmarks/MissionControl.Benchmarks/` (`net11.0`, BenchmarkDotNet, `[MemoryDiagnoser]`), `CompressionBenchmarks.cs`, `StreamWrapBenchmarks.cs`

- **`CompressionBenchmarks`** : échantillon de 1 000 trames `TelemetryCbor.Encode` concaténées ; méthodes `GZip` (baseline), `Brotli`, `Zstandard` qui compressent l'échantillon dans un `MemoryStream`. Une colonne personnalisée (ou un `[GlobalCleanup]` qui écrit dans la console) affiche la taille compressée.
- **`StreamWrapBenchmarks`** : une `ReadOnlySequence<byte>` de 64 Ko répartie sur 16 segments ; `MemoryStreamToArray` (baseline, `new MemoryStream(seq.ToArray())`) face à `ReadOnlySequenceStream`, chacun lu jusqu'au bout dans un buffer de 4 Ko.

Les baselines tournent elles aussi en `net11.0`. Le gain vient de l'API, pas du runtime, et Zstd comme `ReadOnlySequenceStream` n'existent pas en .NET 10.

- [ ] **Step 1: Implémenter** les deux classes.
- [ ] **Step 2: Run** `dotnet run -c Release --project benchmarks/MissionControl.Benchmarks -- --filter *` — Expected: deux tableaux ; la ligne `ReadOnlySequenceStream` affiche moins d'allocations que la baseline. Copier les tableaux dans le runbook.
- [ ] **Step 3: Commit** `feat(benchmarks): Zstd vs Brotli/GZip, streams sans copie`.

---

### Task 14: Runbook de présentation

**Files:**
- Create: `docs/demo-runbook.md`

- [ ] **Step 1: Écrire le runbook** dans l'ordre des étapes 0 à 7 de la spec. Pour chaque étape : la commande ou la requête exacte (fichier `.http` ou `curl`), la paire de fichiers .NET 10 / .NET 11 à ouvrir en vue split, le résultat attendu à l'écran, et la capture de secours à préparer (dotnetup, MCP, WSL). Ajouter les chiffres relevés aux Tasks 5, 12 et 13.
- [ ] **Step 2: Créer un tag git par étape** (`etape-0` … `etape-7`) pour pouvoir revenir à un état connu en direct.
- [ ] **Step 3: Répétition** : dérouler le runbook de bout en bout sur une machine propre. Expected : chaque étape produit le résultat noté.
- [ ] **Step 4: Commit** `docs: runbook de présentation`.
