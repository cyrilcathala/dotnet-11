# Runbook de démo : Mission Control, .NET 11

Script du présentateur. Ordre : étapes 0 à 7 de la spec (`docs/superpowers/specs/2026-11-22-mission-control-dotnet11-design.md`, §5). Toutes les requêtes sont dans `docs/demo.http` (REST Client / Rider / Visual Studio), dans l'ordre des étapes. Chaque bloc donne : quoi lancer, quoi ouvrir en split (.NET 10 à gauche, .NET 11 à droite), ce que le public doit voir, le plan B.

## Avant de monter sur scène

1. `dotnet test MissionControl.slnx` (85 tests, dont la mission complète de bout en bout) et `dotnet build compare/net10` doivent être verts.
2. Lancer l'AppHost : `dotnet run --project src/MissionControl.AppHost --launch-profile https`. Le profil `http` échoue (Aspire exige `ASPIRE_ALLOW_UNSECURED_TRANSPORT`). Le dashboard est sur `https://localhost:17115` (le jeton de connexion est dans la sortie).
3. Relever les ports : `aspire describe --format Table`. Les ports des services sont dynamiques ; les reporter en tête de `docs/demo.http`. MCP est fixe : http 6277, https 5085.
4. Les données sont en mémoire : après un déroulé complet (étape 6), redémarrer l'AppHost pour rejouer (sinon « nom déjà pris »).
5. Tag de retour à un état connu : `git checkout demo-ready` (un seul tag : le code n'est pas découpé par étape, huit tags identiques auraient induit en erreur).
6. Pré-compiler pour éviter l'attente : un premier `dotnet run --file tools/preflight.cs` et `dotnet run --file tools/debris-scan.cs`. Le premier lancement de `preflight.cs` compile le fichier et prend quelques secondes.

## Console visuelle (Blazor)

La ressource `console` de l'AppHost (`src/MissionControl.Web`) sert http://localhost:5194 : une page par étape, avec les scénarios de `docs/demo.http` en boutons, la réponse HTTP affichée en direct, la télémétrie live (jauges, perte de signal à l'étape 6) et les sources .NET 10 et .NET 11 côte à côte, lues dans le dépôt au moment de l'affichage. Les flèches ← → (ou une télécommande de présentation) passent d'une étape à l'autre. La sonde courante se change en haut à droite (`Pioneer-13` pour l'étape 7). La séquence d'agent de l'étape 7 est un plan B scripté : elle passe par les mêmes appels JSON-RPC, mais aucun modèle n'intervient.

## Étape 0 : préparation et AppHost

- **Faire** : installer le SDK 11 (`dotnetup sdk install 11.0.1xx`, puis `dotnet --version` = `11.0.100`), puis lancer l'AppHost (voir ci-dessus, ou `aspire run`). Générer le schéma : `aspire describe --format mermaid` (identique à `docs/architecture.mmd`, à réutiliser en slide).
- **Aspire 17** : les ressources se déclarent avec `AddDotnetProject(name, path)` (`AddProject<T>` est obsolète). `debris-scan` est un `AddExecutable("debris-scan", "dotnet", toolsDirectory, "run", "--file", "debris-scan.cs")`.
- **Ouvrir** : `src/MissionControl.AppHost/AppHost.cs` (pas d'équivalent .NET 10, c'est une version d'Aspire).
- **Public** : le dashboard liste `probefactory`, `launchcontrol`, `probesimulator`, `telemetryreceiver`, `missioncontrol`, `mcp` en `Healthy`, et `debris-scan` en `Finished`. Montrer la page Terminals.
- **Bonus WSL** : depuis WSL, `dotnet dev-certs https --trust` installe le certificat dans WSL et dans Windows (notes de version ASP.NET Core 11).
- **Plan B** : capture de l'installation dotnetup (la chaîne `dotnetup sdk install` n'a jamais été exécutée, le SDK était déjà là) ; capture du dashboard ; capture du HTTPS WSL sans avertissement. Garder le SDK 11 installé à l'avance.

## Étape 1 : ProbeFactory

- **Requêtes** : `docs/demo.http` 1.1 à 1.5. Créer `Pioneer-12`, puis un nom trop court en `Accept-Language: fr` puis `en`, puis le doublon, puis `voyager`.
- **Ouvrir en split** : `compare/net10/Compare.Net10/ProbeFactory/CreateProbeEndpoint.cs` | `src/ProbeFactory/CreateProbeEndpoint.cs`. Puis `compare/net10/Compare.Net10/ProbeFactory/ProbeRegistry.cs` | `src/ProbeFactory/ProbeRegistry.cs` pour `[with(StringComparer.OrdinalIgnoreCase), "Voyager", "Apollo"]`.
- **Public** : 400 avec « Le champ Name doit contenir entre 3 et 32 caractères. » (fr) puis « The Name field must be between 3 and 32 characters. » (en) ; « Ce nom de sonde est déjà pris. » pour le doublon (vérification asynchrone, avant l'endpoint). Côté .NET 10 : `AddValidation()` valide déjà les attributs synchrones, mais les messages passent par `ErrorMessageResourceType` sur chaque attribut et l'unicité est vérifiée à la main dans l'endpoint (pas de validation asynchrone). Les messages viennent de `src/ProbeFactory/Resources/ValidationMessages*.resx`.
- **Plan B** : coller les réponses JSON d'une répétition dans une slide.

## Étape 2 : LaunchControl et preflight.cs

- **Requêtes** : `docs/demo.http` 2.1 (orage, 409), 2.2 (GO, 200), 2.3 (pile du compte à rebours). `preflight.cs` seul, en direct : `dotnet run -e FORCE_STORM=true -e LAUNCH_SITE=Kourou --file tools/preflight.cs` (code de sortie 1, `NO-GO`).
- **Ouvrir en split** : `compare/net10/Compare.Net10/LaunchControl/PreflightRunner.cs` | `src/LaunchControl/PreflightRunner.cs` (`Process.RunAndCaptureTextAsync`). `compare/net10/Compare.Net10/LaunchControl/CountdownSequence.cs` | `src/LaunchControl/CountdownSequence.cs`. Fichier d'outil : `tools/preflight.cs`.
- **Runtime async (point d'exactitude)** : comparer **14 frames sans, 7 frames avec**, sur .NET 11, même endpoint ASP.NET. Sans recompiler : `GET /diagnostics/countdown-stack?runtimeAsync=false` (14) puis `?runtimeAsync=true` (7, la valeur par défaut). Le même `src/LaunchControl/CountdownSequence.cs` est compilé deux fois : dans LaunchControl (`<Features>runtime-async=on</Features>`) et dans `src/LaunchControl.ClassicAsync` (sans), référencé sous l'`extern alias classicasync`. La console (étape 02, bouton « Capturer les deux ») affiche les deux piles côte à côte. Basculer la propriété du csproj reste possible, mais n'est plus nécessaire. Les 13 frames de .NET 10 sont mesurés dans un `Main` console (`dotnet run --project compare/net10/Compare.Net10 -- countdown`) : ce n'est **pas comparable** à l'endpoint, donc ne jamais présenter 13 → 7. Seules les piles **live** (débogueur, profileur, `StackTrace`) changent ; les stack traces d'exception étaient déjà propres.
- **Public** : 409 puis 200 (le suspense de l'orage), puis `"frames": 7`.
- **Plan B** : captures de la pile à 14 frames et de celle à 7 ; captures d'un `NO-GO` puis d'un `GO`.

## Étape 3 : en vol

- **Requêtes** : `docs/demo.http` 3.1 (dernière trame) et 3.2 (compteurs). `duplicates` passe à 1 au bout de quelques secondes (la 5e trame est retransmise).
- **Ouvrir en split** : `compare/net10/Compare.Net10/ProbeSimulator/TelemetryContent.cs` | `src/ProbeSimulator/TelemetryContent.cs` (Zstd à la place de GZip/Brotli) ; `compare/net10/Compare.Net10/TelemetryReceiver/BufferedTelemetryDecoder.cs` | `src/TelemetryReceiver/IncrementalTelemetryDecoder.cs` ; `compare/net10/Compare.Net10/TelemetryReceiver/PacketIdComparer.cs` | `src/TelemetryReceiver/PacketIdComparer.cs` (`EqualityComparer<T>.Create`) ; `compare/net10/Compare.Net10/TelemetryReceiver/IngestEndpoint.cs` | `src/TelemetryReceiver/IngestEndpoint.cs` (boîte noire : `new MemoryStream(buffer.ToArray())` contre `new ReadOnlySequenceStream(buffer)`, sans copie du tampon du pipe).
- **Talking point CBOR** : `System.Formats.Cbor` est inclus dans le framework partagé ASP.NET Core (10 et 11) ; une bibliothèque `Microsoft.NET.Sdk` comme Contracts référence le package NuGet (`11.0.0`). `CborReaderOptions` est nouveau en .NET 11 : `AllowMultipleRootLevelValues = true` est nécessaire pour une suite de trames, et `SlideData(chunk, isFinalBlock)` permet la lecture incrémentale.
- **Benchmarks** : voir la section « Benchmarks » ci-dessous et ses réserves. Relancer : `dotnet run -c Release --project benchmarks/MissionControl.Benchmarks -- --filter * --job short` (plusieurs minutes : pas en direct).
- **Public** : une trame JSON avec `packetId`, `altitudeKm`, `batteryPercent` ; `received` qui monte.
- **Plan B** : captures des tableaux BenchmarkDotNet (reproduits plus bas).

## Étape 4 : interlude, debris-scan

- **Faire** : dans le dashboard, redémarrer la ressource `debris-scan` et ouvrir sa console. Secours en terminal : `dotnet run --file tools/debris-scan.cs`. Sortie attendue : trois lignes « sans danger », puis `Débris en (4, 10) : conjonction dans 4 min — manœuvre d'évitement.`
- **Ouvrir** : `tools/debris-scan.cs`, `tools/RadarSweep.cs`, `tools/Grid.cs`, `tools/Conjunction.cs` (pas de variante .NET 10). Montrer `#:include`, `StringStream`, l'indexeur d'extension `grid[pos]` et `break outer;`.
- **Public** : un fichier, pas de `.csproj`, qui en inclut trois autres. Scénario : une image radar centrée sur la sonde ; le script s'arrête à la première conjonction prédite, comme un évitement de débris pour l'ISS.
- **Plan B** : capture de la console de la ressource.

## Étape 5 : MissionControl

Le contrat de commande : `closed` et union. Le journal JSONL et la trace sont gardés pour l'étape 6.

- **Requêtes** : `docs/demo.http` 5.1 (Acknowledged), 5.2 (Rejected), 5.3, 5.4 (OpenAPI en `anyOf`). Dans la console, un clic sur un scénario préremplit la console de commande ; « Transmettre » l'envoie.
- **Ouvrir en split** : `compare/net10/Compare.Net10/MissionControl/ProbeCommand.cs` | `src/MissionControl.Contracts/ProbeCommand.cs` (`abstract record` + `default: throw` contre `closed record`, chacun avec son `switch` `ToRequest` ; en .NET 10, un `[JsonDerivedType]` par cas, en .NET 11 un seul `[JsonPolymorphic(InferClosedTypePolymorphism = true)]`, sans lister les cas ni leurs identifiants : le discriminant est `"$type": "Thrust"`, le nom du record) ; `compare/net10/Compare.Net10/MissionControl/CommandResult.cs` | `src/MissionControl.Contracts/CommandResult.cs` (hiérarchie contre `union`, chacun avec son `switch` `Describe` ; côté .NET 11, `[JsonUnion]` et son classifieur structurel permettent à la console de relire l'union, voir `src/MissionControl.Web/Services/CommandOutcome.cs`).
- **Démo du `closed`** : coller à la fin de `src/MissionControl.Contracts/ProbeCommand.cs` la ligne `public record Refuel(Guid Id, string ProbeName, double Liters) : ProbeCommand(Id, ProbeName);`. Attendu : avertissement CS8509 (switch non exhaustif) sur le `switch` de `ProbeCommand.ToRequest`, quelques lignes plus haut dans le même fichier. Annuler ensuite : `git checkout -- src/MissionControl.Contracts/ProbeCommand.cs`.
- **Public** : réponses `Acknowledged` / `Rejected` sans discriminant (l'union sérialise le cas directement), et le même résultat décrit en `anyOf` dans l'OpenAPI. Finir sur la branche `ProbeLost` du `switch`, qu'aucune commande n'a encore déclenchée.
- **Plan B** : capture de la réponse et du schéma OpenAPI.

## Étape 6 : incident

L'incident, puis le post-mortem : le journal de bord sert de boîte noire, la trace montre où la mission s'est arrêtée.

- **Requêtes** : `docs/demo.http` 6.1 (`SelfDestruct`, `ProbeLost`), 6.2 (toujours `ProbeLost`), 6.3 (sonde inconnue, `Rejected`), 6.4 (le journal JSONL de toute la mission). En terminal : `curl -N <missioncontrol>/commands/history` (un objet JSON par ligne, réponse en flux ; l'historique courant, puis la réponse se termine).
- **Ouvrir en split** : `compare/net10/Compare.Net10/MissionControl/CommandHistoryEndpoint.cs` | `src/MissionControl.Api/Program.cs` (`SerializeAsyncEnumerable(..., topLevelValues: true)`).
- **OpenTelemetry natif** : le bouton « Ouvrir la trace dans le dashboard » de la console ouvre la trace de la dernière commande (span racine `console POST /commands`). Elle traverse `missioncontrol` puis `probesimulator`, sans `OpenTelemetry.Instrumentation.AspNetCore` côté .NET 11 (le `.csproj` de `compare/net10` l'a). Le lien suppose que le navigateur est déjà connecté au dashboard (ouvert par l'AppHost).
- **Public** : `{"commandId":..., "probeName":"Pioneer-12"}` (le cas `ProbeLost`) ; la télémétrie se tait (LOS) ; le journal retrace ack, rejected, puis lost.
- **Plan B** : capture de la trace.

## Étape 7 : final, serveur MCP

- **Faire** : brancher un client MCP (n'importe lequel) sur `http://localhost:6277/` (transport HTTP, sans état). Outils exposés : `launch_probe`, `send_command`, `get_telemetry`. Avant, créer une sonde neuve `Pioneer-13` : `docs/demo.http` 1.1 après avoir remplacé `@probe = Pioneer-12` par `@probe = Pioneer-13` (`Pioneer-12` est perdue depuis l'étape 6). Demander ensuite à l'agent de lancer `Pioneer-13`, d'envoyer une commande et de lire sa télémétrie.
- **Ouvrir** : `src/MissionControl.Mcp/Program.cs` et `src/MissionControl.Mcp/Tools/MissionTools.cs` (template `dotnet new mcpserver` du SDK).
- **Public** : l'agent appelle les outils, les traces apparaissent dans le dashboard.
- **Plan B** : `docs/demo.http` 7.1 à 7.3 (initialize, tools/list, tools/call) ; capture d'une session d'agent.

## À répéter manuellement

Non exercé en répétition automatisée :

| Élément | Pourquoi | Capture de secours à préparer |
|---|---|---|
| `dotnetup sdk install 11.0.1xx` | SDK déjà installé, jamais lancé | Installation complète dans un terminal |
| `aspire run` | Seul `dotnet run --project src/MissionControl.AppHost --launch-profile https` a été exécuté | Sortie de `aspire run` avec l'URL du dashboard |
| Dashboard (ressources, Terminals, traces, console de `debris-scan`) | Rendu visuel, non vérifiable sans interface | Une capture par page |
| Client MCP réel | Seules les requêtes JSON-RPC ont été jouées | Session d'agent complète |
| WSL, auto-trust des certificats | Non testable ici | HTTPS sans avertissement depuis WSL |
| Avertissement CS8509 à l'ajout de `Refuel`, bascule `runtime-async` en direct | Dépend de l'éditeur | Capture de l'avertissement ; piles 14 et 7 |

## Mesures

Nombre de frames de `GET /diagnostics/countdown-stack` (LaunchControl, .NET 11.0.100, build Debug, `dotnet LaunchControl.dll` puis `curl`) :

| Configuration | Frames |
|---|---|
| Sans runtime async (`runtime-async=off`) | 14 |
| Avec `<Features>runtime-async=on</Features>` | 7 |
| .NET 10.0.403, async classique (`compare/net10`, `dotnet run -- countdown`, console) | 13 |

Frames = lignes de la pile commençant par `   at `. Mesuré en retirant puis remettant la propriété dans `LaunchControl.csproj` ; mêmes valeurs (14 et 7) avec les deux compilations chargées dans le même processus (`?runtimeAsync=false|true`).

À présenter : 14 → 7 sur .NET 11. La ligne .NET 10 vient d'un `Main` console, pas d'un endpoint ASP.NET : elle n'est pas comparable.

## Benchmarks

`dotnet run -c Release --project benchmarks/MissionControl.Benchmarks -- --filter '*' --launchCount 3` (.NET 11.0.0, AMD Ryzen 7 5800X, BenchmarkDotNet 0.16.0-preview.2, job par défaut : itérations adaptatives, 3 lancements de processus, environ 10 minutes au total). Niveau de compression par défaut : `CompressionLevel.Optimal` pour GZip et Brotli, constructeur par défaut `ZstandardStream(Stream, CompressionMode.Compress)` pour Zstd. Échantillon : 1 000 trames CBOR concaténées. Les baselines tournent aussi en `net11.0`.

### Compression

| Method    | Mean     | Error   | StdDev   | Median   | Ratio | RatioSD | Taille  | Gen0   | Allocated | Alloc Ratio |
|---------- |---------:|--------:|---------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| GZip      | 656.9 μs | 7.79 μs | 22.09 μs | 651.1 μs |  1.00 |    0.00 | 17240 o | 2.9297 |  56.71 KB |        1.00 |
| Brotli    | 633.0 μs | 8.38 μs | 23.37 μs | 631.2 μs |  0.96 |    0.05 | 14824 o |      - |  14.68 KB |        0.26 |
| Zstandard | 343.8 μs | 4.48 μs | 18.94 μs | 339.4 μs |  0.52 |    0.03 | 14006 o | 0.4883 |  13.95 KB |        0.25 |

### Flux sans copie (64 Ko, 16 segments, lecture par 4 Ko)

| Method                 | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| MemoryStreamToArray    | 3.104 us | 0.0392 us | 0.1507 us |  1.00 |    0.00 | 3.9063 |   65624 B |       1.000 |
| ReadOnlySequenceStream | 1.268 us | 0.0132 us | 0.0282 us |  0.41 |    0.02 | 0.0038 |      80 B |       0.001 |

### Réserves de lecture

- Pour la compression, seuls le temps et la colonne « Taille » sont exploitables. La colonne `Allocated` reflète la croissance du `MemoryStream` de sortie, et la mémoire native des codecs n'est pas comptée.
- Brotli contre GZip : Brotli est environ 4 % plus rapide (633,0 ± 8,4 μs contre 656,9 ± 7,8 μs, intervalles disjoints), mais le ratio reste à 0,96 ± 0,05. L'écart est mesurable, mais trop faible pour en faire un argument.
- Les temps absolus sont 5 à 10 % plus hauts qu'en ShortRun : la mesure a tourné avec l'AppHost Aspire lancé. Comparer les ratios plutôt que les microsecondes.
- GZip et Brotli sont à `Optimal`, Zstd au niveau par défaut : les niveaux ne sont pas équivalents.
- Le résultat robuste est celui des flux : 80 o contre 65 624 o alloués. C'est le chiffre à citer.
