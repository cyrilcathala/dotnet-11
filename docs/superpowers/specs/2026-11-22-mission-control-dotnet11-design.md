# Mission Control — démos .NET 11 : choix de conception

_Date : 22 novembre 2026 — statut : validé pour l'écriture des scénarios_

## 1. Intention

Présenter les nouveautés de .NET 11 (sorti le 10 novembre 2026, STS) à travers une suite de démos sur un même thème : un **Mission Control** qui construit, lance et pilote des sondes spatiales par des commandes, et reçoit leur télémétrie au format **CBOR**.

Chaque nouveauté est montrée **avec son équivalent .NET 10**, pour que le public voie concrètement le gain (syntaxe, lisibilité, performance).

## 2. Objectifs, non-objectifs, anti-objectifs

**Objectifs**
- Une démo fluide pour le présentateur : code pré-écrit, révélé par étapes, pas de frappe en direct.
- Une démo facile à suivre pour le public : un écran = un fichier = une idée.
- Une histoire continue (une mission, dans l'ordre chronologique) qui relie toutes les nouveautés.
- Un comparatif .NET 10 / .NET 11 lisible côte à côte.

**Non-objectifs**
- Une architecture microservices « réaliste » (résilience, sécurité, persistance réelle).
- Couvrir toutes les nouveautés de .NET 11.
- Benchmarker le JIT, le décodage CBOR entre runtimes ou le démarrage de la CLI.

**Anti-objectifs** (échec même si ça marche)
- Une démo qui dépend du réseau de la salle (API externe, Wi-Fi).
- Une démo dont une étape cassée bloque toutes les suivantes.
- Des packages externes ajoutés uniquement pour le comparatif (pas de OneOf, pas de `Results<…>` pour illustrer les unions).
- Des `#if NET11_0_OR_GREATER` à l'écran.

## 3. Approches envisagées

**Format de démo**

| Option | Principe | Verdict |
|---|---|---|
| A. Appli fil rouge unique | Une solution Aspire qui grandit par tags git | Rejetée : code éparpillé, une panne bloque la suite, features de langage forcées |
| B. Mini-démos isolées | Un fichier `dotnet run` par feature | Rejetée : Aspire / OpenTelemetry / MCP n'y rentrent pas |
| **C. Hybride** | Un Mission Control Aspire découpé en petits services + des « outils de bord » en fichiers seuls | **Retenue** |

**Comparatif .NET 10 / .NET 11**

| Option | Principe | Verdict |
|---|---|---|
| **1. Fichiers en paires + benchmark séparé** | Fichier .NET 10 et fichier .NET 11 en vue split ; BenchmarkDotNet multi-runtime pour la perf | **Retenue** |
| 2. Multi-ciblage + `#if` | Un seul code `net10.0;net11.0` | Rejetée : les `#if` brouillent l'écran |
| 3. Tags git avant / après | Diff entre deux tags | Rejetée : un diff se lit mal au fond de la salle |

Quand une API n'existe pas en .NET 10 (Zstd, lecture CBOR incrémentale…), on compare à **l'ancienne manière de faire**, pas à la même API.

## 4. Architecture

```
AppHost (Aspire 17)
 ├─ ProbeFactory        création de sonde
 ├─ LaunchControl       lancement (appelle preflight.cs)
 ├─ ProbeSimulator      sondes en vol, émettent la télémétrie CBOR
 ├─ TelemetryReceiver   réception et décodage de la télémétrie
 ├─ MissionControl      API de commandes + historique JSONL
 ├─ MissionControl.Mcp  serveur MCP piloté par un agent IA
 └─ asteroid-scan       outil de bord (fichier seul) lancé depuis Aspire

Outils de bord (file-based apps) : preflight.cs, asteroid-scan.cs (+ SectorMap.cs, Grid.cs)
Benchmarks : projet BenchmarkDotNet, runtimes .NET 10 et .NET 11
```

## 5. Déroulé chronologique et nouveautés par étape

### Étape 0 — Préparation et AppHost (ouverture de la démo)

Le présentateur part d'une machine « vierge » côté .NET 11 et démarre le centre de contrôle.

| Nouveauté | Démo | Comparatif .NET 10 |
|---|---|---|
| **dotnetup** (preview publique) | Installer le SDK 11 en direct | Script `dotnet-install` / installeur classique |
| **Aspire 17** (17.0.0, 17 nov. 2026) | `aspire run` : nouveau dashboard (accueil en tableau des ressources, page Terminals) ; tous les services apparaissent | — (version d'Aspire, pas de .NET) |
| `aspire describe --format mermaid` | Génère le schéma de l'architecture Mission Control, réutilisé comme slide | — |
| **Bonus** : auto-trust des certificats de dev sous WSL | Lancer l'AppHost depuis WSL, HTTPS fonctionne sans manipulation | Export / import manuel du certificat |

Avec Aspire 17, les services se déclarent avec `AddDotnetProject(name, path)` (`AddProject<T>` est obsolète) :

```csharp
var probefactory = builder.AddDotnetProject("probefactory", "../ProbeFactory/ProbeFactory.csproj")
    .WithHttpEndpoint();
```

L'AppHost déclare aussi `asteroid-scan` comme exécutable :

```csharp
builder.AddExecutable("asteroid-scan", "dotnet", toolsDirectory, "run", "--file", "asteroid-scan.cs");
```

### Étape 1 — Construire la sonde : `ProbeFactory`

| Nouveauté | Démo | Comparatif .NET 10 |
|---|---|---|
| **Validation async** (`AsyncValidationAttribute`, `AddValidation()`) | Le nom de sonde doit être unique dans le registre ; la vérification est asynchrone et s'exécute avant l'endpoint | Vérification manuelle dans le corps de l'endpoint |
| **Localisation des messages de validation** (`AddLocalization()` + `AddValidation()`, clés par convention `{Type}_{Membre}_{Attribut}_Error`) | Même requête invalide avec `Accept-Language: fr` puis `en` | `ErrorMessageResourceType` répété sur chaque attribut |
| **Arguments de collection expressions** | `HashSet<string> reserved = [with(StringComparer.OrdinalIgnoreCase), "Voyager", "Apollo"];` | `var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Voyager", "Apollo" };` |

### Étape 2 — Le décollage : `LaunchControl` + `preflight.cs`

Avant chaque lancement, LaunchControl exécute un outil de vérification météo du pas de tir.

```csharp
var psi = new ProcessStartInfo("dotnet",
    ["run", "-e", "LAUNCH_SITE=Kourou", "-e", $"PROBE={probe.Name}", "--file", "preflight.cs"])
{
    RedirectStandardOutput = true, RedirectStandardError = true,       // requis par la surcharge psi
    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, // l'outil écrit en UTF-8
};
var result = await Process.RunAndCaptureTextAsync(psi, ct);

return result.ExitStatus.ExitCode == 0 ? Go(result.StandardOutput) : NoGo(result.StandardOutput);
```

- `preflight.cs` est une **station météo simulée** (pas d'API externe) : il lit ses variables d'environnement, affiche quelques relevés (vent, orages, nuages) et renvoie 0 (GO) ou 1 (NO-GO).
- `-e FORCE_STORM=true` force un NO-GO reproductible. **Moment de suspense** : le premier lancement est bloqué par l'orage, le second passe.

| Nouveauté | Démo | Comparatif .NET 10 |
|---|---|---|
| **API `Process` one-shot** (`Process.RunAndCaptureTextAsync`, `ProcessTextOutput`) | L'appel ci-dessus | `ProcessStartInfo` + redirections + `ReadToEndAsync` + `WaitForExitAsync` |
| **`dotnet run -e`** | Variables passées sur la ligne de commande de l'outil | Variables via `psi.Environment[...]` |
| **Runtime async** (`<Features>runtime-async=on</Features>`) | Pile d'appels **live** (`new StackTrace()` ou débogueur) pendant la séquence de compte à rebours | Même code sur .NET 11 : 14 frames sans, 7 avec (mesuré sur l'endpoint ASP.NET). Le 13 de .NET 10 vient d'un `Main` console : ne pas le comparer directement |

> Cadrage runtime async : seules les piles **live** (débogueur, profileur, `StackTrace`) changent. Les stack traces d'exception étaient déjà propres ; ne pas les présenter comme la nouveauté.

### Étape 3 — En vol : `ProbeSimulator` → `TelemetryReceiver`

| Nouveauté | Démo | Comparatif .NET 10 |
|---|---|---|
| **Compression Zstandard** | Le simulateur envoie sa télémétrie compressée en Zstd ; il suffit d'activer Zstd | GZip ou Brotli |
| **Lecture CBOR incrémentale** (`CborReader` avec `isFinalBlock: false`, `PeekState()` == `NeedsMoreData`, `SlideData(chunk, isFinalBlock)`, mode `CborConformanceMode.Lax` obligatoire) | Les morceaux sont décodés au fur et à mesure de leur arrivée | Tout mettre en tampon, puis parser d'un bloc |
| **`ReadOnlySequenceStream`** | Le buffer d'un `PipeReader` exposé en `Stream` sans copie | `new MemoryStream(sequence.ToArray())` |
| **`EqualityComparer<T>.Create(p => p.PacketId)`** | Dédoublonnage des paquets retransmis dans un `HashSet` | Classe dédiée qui implémente `IEqualityComparer<T>` |

Le CBOR est présenté brièvement (format binaire compact, cf. [article d'Andrew Lock](https://andrewlock.net/an-introduction-to-compact-binary-object-notation-in-dotnet/)).

**Benchmarks montrés à cette étape** (BenchmarkDotNet, .NET 10 et .NET 11) :
- Zstd face à Brotli et GZip sur un échantillon de télémétrie : taille et temps.
- `ReadOnlySequenceStream` face à `MemoryStream(ToArray())` : allocations.

### Étape 4 — Interlude : champ d'astéroïdes, `asteroid-scan.cs`

Lancé depuis le dashboard Aspire. Pas de variante .NET 10.

```csharp
// SectorMap.cs
static class SectorMap
{
    public const string Kuiper = """
        ..........#.....
        ....##..........
        .........#...P..
        """;
}

// asteroid-scan.cs
#:include SectorMap.cs
#:include Grid.cs

using var map = new StringStream(SectorMap.Kuiper, Encoding.UTF8);
var grid = await Grid.LoadAsync(map);   // le parseur accepte n'importe quel Stream
```

| Nouveauté | Démo |
|---|---|
| **`#:include`** | La carte et la grille sont dans des fichiers séparés, toujours sans `.csproj` |
| **`StringStream`** | La carte codée en dur est lue comme un flux capteur, encodée à la volée |
| **Indexeur d'extension** | `grid[pos]` |
| **`break` / `continue` étiquetés** | `break outer;` dès qu'un astéroïde est détecté sur la trajectoire |

Les trois autres nouveaux streams (`ReadOnlyMemoryStream`, `WritableMemoryStream`, `ReadOnlySequenceStream`) sont rappelés sur une slide.

### Étape 5 — Piloter les sondes : `MissionControl`

```csharp
public closed record ProbeCommand(Guid Id, string ProbeName);
public record Thrust(...) : ProbeCommand(...);        // + Rotate, CapturePhoto, SelfDestruct

public union CommandResult(Acknowledged, Rejected, TimedOut, ProbeLost);
```

| Nouveauté | Démo | Comparatif .NET 10 (classes abstraites, sans package) |
|---|---|---|
| **Hiérarchie `closed`** | `switch` exhaustif sur la commande ; ajouter `Refuel` fait apparaître un avertissement | `abstract record ProbeCommand` : n'importe qui peut dériver, `default: throw` obligatoire, un cas oublié ne se voit qu'à l'exécution |
| **Union types** | `return new Acknowledged(...)` par conversion implicite ; `switch` exhaustif sur le résultat ; l'OpenAPI décrit le résultat en `anyOf` | `abstract record CommandResult` dont chaque cas doit hériter ; en .NET 11 les cas sont des types indépendants, réutilisables ailleurs |
| **OpenTelemetry natif dans ASP.NET Core** | La trace d'une commande traverse les services dans le dashboard Aspire, sans `OpenTelemetry.Instrumentation.AspNetCore` | Package d'instrumentation + configuration |
| **Bonus** : JSONL via `JsonSerializer.SerializeAsyncEnumerable(..., topLevelValues: true)` | `GET /commands/history` diffuse l'historique des commandes exécutées ; suivi avec `curl` | Boucle qui sérialise chaque élément et écrit les sauts de ligne à la main |

### Étape 6 — Incident

Une commande `SelfDestruct` (ou une perte de contact) produit `ProbeLost`. Le lien `closed` → union se raconte tout seul, et la trace OpenTelemetry montre où la mission s'est arrêtée.

### Étape 7 — Final : `MissionControl.Mcp`

| Nouveauté | Démo | Comparatif .NET 10 |
|---|---|---|
| **Template MCP server dans le SDK** (`dotnet new mcpserver`) | Outils `LaunchProbe`, `SendCommand`, `GetTelemetry` qui appellent l'API ; un agent IA reprend la mission en main | Pack de templates à installer à part |

## 6. Contraintes

- Cible `net11.0`, C# 15 (langage par défaut). Aspire 17 exige au minimum .NET 10.
- Aucune dépendance réseau externe pendant la démo (hors installation dotnetup, qui doit avoir un plan B).
- Aucun package ajouté uniquement pour le comparatif.
- Les fichiers .NET 10 du comparatif doivent compiler en `net10.0` (crédibilité devant le public).

## 7. Résultats de vérification

Vérifié le 2026-11-22 avec le SDK 11.0.100 (GA), sous Windows. Statut : confirmé / ajusté / retiré. Provenance indiquée par ligne : « spike exécuté » (compilé et lancé), « réflexion » (membres observés, non appelés), « doc » (Microsoft Learn, non mesuré) ou « aide CLI » (`--help`, non exécuté).

| Point | Statut | Résultat (forme exacte qui fonctionne) |
|---|---|---|
| Commandes **dotnetup** (0.3.0-preview) | confirmé (aide CLI, non exécuté) | Installer : `dotnetup sdk install <channel>` (alias `dotnetup install`), ex. `dotnetup sdk install 11.0.1xx` ; options utiles `--set-default-install`, `--install-path`, `--update-global-json`, `--untracked`. Mettre à jour : `dotnetup update` (ou `dotnetup sdk update`). Lister : `dotnetup list [--format Json]`. Aussi : `dotnetup runtime install`, `dotnetup env`, `dotnetup init`, `dotnetup dotnet <args>`. Seul `dotnet --version` (= `11.0.100`) a été exécuté ; la chaîne `dotnetup sdk install 11.0.1xx` n'a pas été lancée (SDK déjà installé). |
| **Auto-trust des certificats WSL** | confirmé (doc, non testé) | Présent dans les notes de version ASP.NET Core 11 (section « Auto-trust development certificates in WSL »), absent de celles du SDK : `dotnet dev-certs https --trust` lancé dans WSL installe le certificat dans WSL et dans Windows. Non testable sur cette machine ; garder une capture de secours. |
| Statut des **unions** et de **`closed`** | confirmé (spike exécuté) | Aucun `#:property LangVersion=preview` nécessaire : `public union CommandResult(A, B);` et `public closed record` compilent avec `dotnet run fichier.cs`. Conversion implicite `CommandResult r = new Acknowledged(..)` OK ; `switch` sur l'union ou sur la hiérarchie `closed` : avertissement CS8509 si un cas manque. Observé par réflexion : constructeurs `(A)` et `(B)`, propriété `Value` (`object?`), interface `IUnion`, attribut `UnionAttribute`. **Non testé** : désérialisation JSON d'une union et rendu OpenAPI en `anyOf` (à vérifier en Task 8). |
| API **Zstd** côté ASP.NET Core | ajusté (requête : spike exécuté ; réponse : réflexion + doc) | `AddRequestDecompression()` sans option gère `Content-Encoding: zstd` par défaut (testé : corps compressé avec `ZstandardStream`, renvoyé décompressé en 200). Pas d'options de décompression zstd dédiées : `RequestDecompressionOptions` n'a que `DecompressionProviders` (`IDictionary<string, IDecompressionProvider>`). Côté réponse : `ZstandardCompressionProvider` et `ZstandardCompressionProviderOptions.CompressionOptions` (`ZstandardCompressionOptions`, propriété `Quality` : vue dans la doc, non compilée). |
| **OpenTelemetry** d'ASP.NET Core en .NET 10 | confirmé (partiel) | Spike exécuté en net10.0 et net11.0 : l'activité serveur `Microsoft.AspNetCore.Hosting.HttpRequestIn` est émise (source `Microsoft.AspNetCore`) dans les deux. Le besoin du package `OpenTelemetry.Instrumentation.AspNetCore` en .NET 10 pour les attributs de convention sémantique est (doc, non mesuré). En .NET 11 le framework renseigne `http.request.method`, `url.path`, `http.response.status_code`, `server.address` (doc) ; `.AddSource("Microsoft.AspNetCore")` suffit (nom d'affichage de l'activité `GET` en 11, mesuré). Désactivation : AppContext `Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData`. |
| API de formatage de la **validation localisée** | ajusté (réflexion ; `LocalizerProvider` compilé) | `IValidationMessageFormatter` n'existe pas. Existent : `ValidationOptions.LocalizerProvider` de type `Func<Type, IStringLocalizerFactory, IStringLocalizer>` (`AddLocalization()` puis `AddValidation(o => o.LocalizerProvider = (_, f) => f.Create(typeof(X)))`, compilé) et `ValidationAttribute.FormatErrorMessage(string)` / `FormatMessage(string, string)` (publiques d'après la réflexion ; ni appelées ni compilées). |
| **`AsyncValidationAttribute`** | ajusté (spike exécuté) | Classe de base abstraite : surcharger les deux membres protégés `ValidationResult? IsValid(object? value, ValidationContext ctx)` (obligatoire, peut lever) et `Task<ValidationResult?> IsValidAsync(object? value, ValidationContext ctx, CancellationToken ct)`. Appel direct : `GetValidationResultAsync(object, ValidationContext, CancellationToken)` (public). Compilé et exécuté. |
| **`dotnet run -e`** | confirmé (spike exécuté) | `dotnet run -e X=1 spikes/env.cs` affiche `1` ; `dotnet run -e X=1 --file spikes/env.cs` aussi (`-e NOM=VALEUR`). |
| **CBOR incrémental** | ajusté (spike exécuté) | `System.Formats.Cbor` est inclus dans le framework partagé ASP.NET Core (`Microsoft.AspNetCore.App`, en .NET 10 comme en .NET 11), mais pas dans `Microsoft.NETCore.App` : un fichier ou une bibliothèque `Microsoft.NET.Sdk` (comme Contracts) référence le package NuGet (`#:package System.Formats.Cbor@11.0.0`). `CborReader.SlideData(ReadOnlyMemory<byte> data, bool isFinalBlock)` ; constructeur `CborReader(ReadOnlyMemory<byte>, CborReaderOptions, bool isFinalBlock)`. `CborReaderOptions` : `ConformanceMode`, `AllowMultipleRootLevelValues`, `MaxDepth`. `AllowMultipleRootLevelValues = true` est nécessaire pour une suite de trames (sinon seule la première est lue, puis état `Finished`). Boucle validée en morceaux de 7 octets : `TrySkipValue(false)` renvoie `false` tant que la trame est incomplète, `BytesRemaining` donne les octets non consommés à conserver avant `SlideData`. Les états `NeedsMoreData` et `Finished` existent. Écriture : `new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false, allowMultipleRootLevelValues: true)`. |

### Résultats de vérification (Task 1)

- **Applications fichier + SDK Web** : `#:sdk Microsoft.NET.Sdk.Web` fonctionne, mais le mode fichier active `PublishAot` par défaut, donc la sérialisation JSON par réflexion est désactivée (500 sur les corps JSON, `Reflection-based serialization has been disabled`). Ajouter `#:property PublishAot=false` (ou un contexte source-generated).
- **`Process.RunAndCaptureTextAsync`** : `Task<ProcessTextOutput> RunAndCaptureTextAsync(string fileName, IEnumerable<string>? arguments = null, CancellationToken cancellationToken = default)` ; surcharge `(ProcessStartInfo, CancellationToken)`. Autres : `Run`, `RunAsync` (`ProcessExitStatus`), `RunAndCaptureText` (synchrone, `TimeSpan? timeout`). `ProcessTextOutput` : `ExitStatus` (`ProcessExitStatus` : `ExitCode`, `Signal`, `Canceled`), `StandardOutput`, `StandardError`, `ProcessId`. `await Process.RunAndCaptureTextAsync("dotnet", ["--version"])` donne `11.0.100`.
- **Valeur d'une union** : propriété `.Value` (`object?`) ou pattern matching (`r switch { Ack a => .. }`). Conversion implicite depuis chaque cas. System.Text.Json sérialise directement le cas (`{"M":"ok"}`), sans discriminant.
- **`[JsonPolymorphic(InferClosedTypePolymorphism = true)]`** : existe et fonctionne sur un `closed record` : `{"$type":"Thrust","P":2.5,"Id":1}`, aller-retour correct vers `Thrust`. Autres propriétés : `TypeDiscriminatorPropertyName`, `UnknownDerivedTypeHandling`, `IgnoreUnrecognizedTypeDiscriminators`, `TypeClassifier`, `TypeId`.
- **`StringStream`** : `System.IO`, public, `new StringStream(string value, Encoding encoding)` ou `(ReadOnlyMemory<char>, Encoding)`.
- **`EqualityComparer<T>.Create`** : `Create<TKey>(Func<T,TKey> keySelector, IEqualityComparer<TKey>? keyComparer = null)` et `Create(Func<T,T,bool> equals, Func<T,int>? getHashCode = null)`.
- **`ZstandardStream`** (`System.IO.Compression`) : `(Stream, CompressionMode[, bool leaveOpen])`, `(Stream, CompressionLevel[, bool leaveOpen])`, `(Stream, ZstandardCompressionOptions, bool leaveOpen)`, `(Stream, ZstandardEncoder, bool)`, `(Stream, ZstandardDecoder, bool)`, `(Stream, ZstandardDecompressionOptions, bool)`, `(Stream, CompressionMode, ZstandardDictionary, bool)`.
- **`ReadOnlySequenceStream`** : espace de noms `System.Buffers` (assembly System.Memory), `new ReadOnlySequenceStream(ReadOnlySequence<byte>)`, non seekable (`CanSeek` faux, `Length` lève).
- **`JsonSerializer.SerializeAsyncEnumerable<T>(Stream, IAsyncEnumerable<T>, bool topLevelValues, JsonSerializerOptions? = null, CancellationToken = default)`** : confirmé (surcharges `PipeWriter` et `JsonTypeInfo<T>` aussi) ; un objet JSON par ligne.
- **`dotnet run`** : accepte `--file` pour un fichier `.cs`.

## 8. Laissé au développeur (mes choix, modifiables)

- **Transport de la télémétrie** entre simulateur et récepteur (HTTP en streaming ou TCP) : au choix, tant que les données arrivent par morceaux pour justifier la lecture incrémentale.
- **Rangement du comparatif** : une solution `net10/` qui ne contient que les fichiers « avant » nécessaires, compilée en `net10.0`, à côté de la solution .NET 11.
- **Lecture de la télémétrie par le MCP** : `TelemetryReceiver` expose un petit endpoint de lecture (dernières valeurs par sonde) appelé par l'outil `GetTelemetry`.
- **Périmètre bonus** (à couper si le temps manque) : lecture CBOR incrémentale, `dotnet run -e`, certificats WSL, OpenTelemetry natif, template MCP, JSONL.
- **Stockage** : en mémoire partout (registre des sondes, historique des commandes).
- **Révélation du code** : tags git par étape ou blocs à décommenter, plus une capture de secours pour chaque démo à risque (dotnetup, MCP, WSL).
- **Agent IA du final** : n'importe quel client MCP disponible sur la machine du présentateur.

## 9. Sources

- [Announcing .NET 11](https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/)
- [What's new in .NET 11 — libraries](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries)
- [What's new in .NET 11 — SDK](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/sdk)
- [What's new in .NET 11 — runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime)
- [What's new in ASP.NET Core 11](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11)
- [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- [What's new in Aspire 17](https://aspire.dev/whats-new/aspire-17/)
- [What's New in .NET 11 — Milan Jovanovic](https://milanjovanovic.tech/blog/whats-new-dotnet-11)
