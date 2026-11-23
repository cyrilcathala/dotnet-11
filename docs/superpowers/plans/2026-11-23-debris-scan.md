# Débris en orbite (étape 4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer l'interlude `asteroid-scan` par `debris-scan`, un outil qui balaye une image radar centrée sur la sonde, prédit la trajectoire de chaque débris et s'arrête (`break outer`) au premier qui menace la sonde.

**Architecture:** Trois fichiers partagés dans `tools/` (`Grid.cs` inchangé, `RadarSweep.cs` pour la carte, `Conjunction.cs` pour la prédiction simulée) sont inclus par le script `debris-scan.cs` via `#:include`, et compilés par lien dans `Tools.Tests` et `MissionControl.Web`. La page `/debris` utilise donc la même carte et les mêmes vitesses que le script.

**Tech Stack:** .NET 11 (file-based apps, `#:include`, `StringStream`, blocs `extension`), xUnit v3, Blazor, Aspire 17.

**Spec:** `docs/superpowers/specs/2026-11-23-debris-scan-design.md`

## Global Constraints

- `asteroid-scan` devient `debris-scan` partout, sauf dans les specs et plans datés du 2026-11-22 (historique, ne pas toucher).
- `tools/Grid.cs` reste inchangé.
- Les quatre nouveautés restent visibles dans `debris-scan.cs` : `#:include`, `StringStream`, `grid[pos]`, `break outer;`.
- Textes en français. Lignes de sortie exactes :
  - `Débris en ({row}, {col}) : trajectoire divergente, sans danger.`
  - `Débris en ({row}, {col}) : conjonction dans {n} min — manœuvre d'évitement.`
  - `Secteur dégagé.` (aucune menace)
- Image radar : cases de 1 km, sonde `P` au centre.

## Carte et vitesses (décidées ici)

```
row 0  ..#........     (0, 2)  vitesse (0, +1)   sans danger
row 1  ........#..     (1, 8)  vitesse (0, +1)   sans danger
row 2  .#.........     (2, 1)  vitesse (+1, +1)  sans danger (passe à 2 km)
row 3  .....P.....     sonde en (3, 5)
row 4  ..........#     (4, 10) vitesse (0, -1)   conjonction à 4 min  ← break outer
row 5  ...........
row 6  ...#.......     (6, 3)  vitesse (+1, 0)   jamais évalué (et sans danger)
```

Règle de `Conjunction.Predict` : pour t = 1 … `HorizonMinutes` (5), position = débris + t × vitesse ; renvoie le premier t où `max(|Δligne|, |Δcolonne|) ≤ 1` avec la sonde, sinon `null`. Un débris absent de la table a une vitesse (0, 0).

Le débris non évalué `(6, 3)` doit rester sans danger : s'il menaçait plus tôt que `(4, 10)`, la démo laisserait croire que l'outil rate la menace la plus urgente.

## Review Focus

1. Trajectoire qui sort de la grille (ex. `(1, 8)` atteint la colonne 13) : la page ne doit ni planter ni dessiner hors grille → clipping dans `Debris.razor`, vérifié à l'écran (Task 3, étape 5).
2. Débris absent de la table des vitesses : considéré immobile, menaçant seulement s'il est déjà à 1 km → test `Unknown_debris_is_static` (Task 1).
3. Le débris non évalué apparaît dans la sortie (le `break` ne sort que de la boucle interne) → assertion `DoesNotContain("(6, 3)")` (Task 2).
4. Carte modifiée plus tard qui casse la démo (menace en premier, ou rien après) → test `Sweep_has_safe_echoes_before_threat_and_one_after` (Task 1).
5. Encodage du tiret cadratin et des accents dans la console Aspire et la page → `Console.OutputEncoding = UTF8` conservé, assertion sur la ligne exacte (Task 2).

---

### Task 1: Carte radar et prédiction de conjonction

**Files:**
- Create: `tools/Conjunction.cs`
- Rename: `tools/SectorMap.cs` → `tools/RadarSweep.cs` (`git mv`)
- Modify: `tests/Tools.Tests/Tools.Tests.csproj` (liens de compilation)
- Create: `tests/Tools.Tests/ConjunctionTests.cs`

**Interfaces:**
- Consumes: `Position(int Row, int Col)`, `Grid.LoadAsync(Stream)`, indexeur `grid[Position]` de `tools/Grid.cs`.
- Produces:
  - `static class RadarSweep { public const string Current; }` (chaîne brute de la carte ci-dessus, avec un commentaire : image radar centrée sur la sonde, cases de 1 km, `#` = écho, `P` = sonde).
  - `public static class Conjunction`
    - `public const int HorizonMinutes = 5;`
    - `public static IReadOnlyList<Position> Path(Position debris)` : positions prévues pour t = 1 … 5 (sans bornes de grille).
    - `public static int? Predict(Position debris, Position probe)` : minute de conjonction ou `null`.
  - La table des vitesses est privée (`Dictionary<Position, (int Rows, int Cols)>`), valeurs ci-dessus.

- [ ] **Step 1: Lier les fichiers dans le projet de test**

Dans `Tools.Tests.csproj` : `<Compile Include="../../tools/Grid.cs" Link="Shared/Grid.cs" />`, idem `Conjunction.cs` et `RadarSweep.cs`.

- [ ] **Step 2: Écrire les tests**

```csharp
public class ConjunctionTests
{
    static readonly Position Probe = new(3, 5);

    [Fact] public void Threatening_debris_meets_probe_at_minute_4()
        => Assert.Equal(4, Conjunction.Predict(new(4, 10), Probe));

    [Theory, InlineData(0, 2), InlineData(1, 8), InlineData(2, 1), InlineData(6, 3)]
    public void Other_debris_are_safe(int row, int col)
        => Assert.Null(Conjunction.Predict(new(row, col), Probe));

    [Fact] public void Unknown_debris_is_static()
    {
        Assert.Equal(1, Conjunction.Predict(new(2, 4), Probe));
        Assert.Null(Conjunction.Predict(new(0, 0), Probe));
    }

    [Fact] public void Path_has_one_position_per_minute()
        => Assert.Equal([new(4, 9), new(4, 8), new(4, 7), new(4, 6), new(4, 5)], Conjunction.Path(new(4, 10)));

    [Fact] public async Task Sweep_has_safe_echoes_before_threat_and_one_after()
    {
        using var s = new StringStream(RadarSweep.Current, System.Text.Encoding.UTF8);
        var grid = await Grid.LoadAsync(s);
        var cells = grid.SelectMany((line, r) => line.Select((ch, c) => (ch, pos: new Position(r, c)))).ToList();
        Assert.Equal([Probe], cells.Where(x => x.ch == 'P').Select(x => x.pos));
        var echoes = cells.Where(x => x.ch == '#').Select(x => x.pos).ToList();
        var first = echoes.FindIndex(p => Conjunction.Predict(p, Probe) is not null);
        Assert.True(first >= 2);
        Assert.True(first < echoes.Count - 1);
    }
}
```

- [ ] **Step 3: Lancer, vérifier l'échec**

Run: `dotnet test tests/Tools.Tests/Tools.Tests.csproj`
Expected: échec de compilation (`Conjunction`, `RadarSweep` introuvables).

- [ ] **Step 4: Implémenter**

`git mv tools/SectorMap.cs tools/RadarSweep.cs`, remplacer la classe `SectorMap.Kuiper` par `RadarSweep.Current` avec la carte ci-dessus. Créer `Conjunction.cs` selon la règle de la section « Carte et vitesses ».

- [ ] **Step 5: Lancer, vérifier le succès**

Run: `dotnet test tests/Tools.Tests/Tools.Tests.csproj`
Expected: `ConjunctionTests` au vert. `AsteroidScanTests` échoue (le script référence encore `SectorMap`) : c'est attendu, Task 2 le remplace.

- [ ] **Step 6: Commit**

`feat(debris): carte radar et prédiction de conjonction simulée`

### Task 2: Script `debris-scan.cs` et renommage côté outil

**Files:**
- Rename: `tools/asteroid-scan.cs` → `tools/debris-scan.cs`
- Rename: `tests/Tools.Tests/AsteroidScanTests.cs` → `tests/Tools.Tests/DebrisScanTests.cs`
- Modify: `src/MissionControl.AppHost/AppHost.cs:30`
- Modify: `docs/demo-runbook.md` (lignes 12, 21, 23, 51-56, 88), `docs/demo.http:88-89`, `docs/architecture.mmd:2`

**Interfaces:**
- Consumes: `RadarSweep.Current`, `Conjunction.Predict` (Task 1).
- Produces: la ressource Aspire `debris-scan` (`AddExecutable("debris-scan", "dotnet", toolsDirectory, "run", "--file", "debris-scan.cs")`).

- [ ] **Step 1: Écrire le test**

```csharp
public class DebrisScanTests
{
    [Fact]
    public async Task Scan_stops_at_first_threatening_debris()
    {
        // même ToolPath et même lancement que l'ancien AsteroidScanTests, fichier "debris-scan.cs"
        Assert.Equal(0, r.ExitStatus.ExitCode);
        Assert.Contains("Débris en (0, 2) : trajectoire divergente, sans danger.", r.StandardOutput);
        Assert.Contains("Débris en (2, 1) : trajectoire divergente, sans danger.", r.StandardOutput);
        Assert.Contains("Débris en (4, 10) : conjonction dans 4 min — manœuvre d'évitement.", r.StandardOutput);
        Assert.DoesNotContain("(6, 3)", r.StandardOutput);
    }
}
```

- [ ] **Step 2: Lancer, vérifier l'échec**

Run: `dotnet test tests/Tools.Tests/Tools.Tests.csproj`
Expected: FAIL, `debris-scan.cs` introuvable.

- [ ] **Step 3: Écrire le script**

`git mv tools/asteroid-scan.cs tools/debris-scan.cs`. Trois `#:include` (`RadarSweep.cs`, `Grid.cs`, `Conjunction.cs`). Lecture par `StringStream`. Repérer la sonde (`IndexOf('P')` ligne par ligne). Boucles imbriquées étiquetées `outer:` : `if (grid[pos] != '#') continue;`, puis `Conjunction.Predict(pos, probe)` ; sans danger → ligne « divergente » ; menace → ligne « conjonction », `break outer;`. Après la boucle : `Secteur dégagé.` si aucune menace. Garder `Console.OutputEncoding = Encoding.UTF8`.

- [ ] **Step 4: Lancer, vérifier le succès**

Run: `dotnet test tests/Tools.Tests/Tools.Tests.csproj`
Expected: tous les tests au vert.
Run: `dotnet run --file tools/debris-scan.cs`
Expected : trois lignes « divergente » puis la ligne de conjonction pour `(4, 10)`.

- [ ] **Step 5: Renommer dans AppHost et la doc**

`AppHost.cs:30` → `debris-scan`. Runbook : étape 4 titrée « interlude, debris-scan », sortie attendue = la ligne de conjonction, fichiers à ouvrir `debris-scan.cs`, `RadarSweep.cs`, `Grid.cs`, `Conjunction.cs`, plus une phrase de scénario (image radar centrée sur la sonde, évitement de débris comme l'ISS). `demo.http` et `architecture.mmd` : remplacer le nom.

Run: `dotnet build src/MissionControl.AppHost`
Expected: build OK.

- [ ] **Step 6: Commit**

`feat(debris): debris-scan remplace asteroid-scan, sortie à la première conjonction`

### Task 3: Page `/debris` de la console Blazor

**Files:**
- Rename: `src/MissionControl.Web/Components/Pages/Asteroides.razor` → `Debris.razor`
- Modify: `src/MissionControl.Web/MissionControl.Web.csproj` (liens `Grid.cs`, `Conjunction.cs`, `RadarSweep.cs`, même forme qu'en Task 1)
- Modify: `src/MissionControl.Web/Services/Mission.cs:18-19`
- Modify: `src/MissionControl.Web/Components/Pages/Preparation.razor:49,81,106,126`
- Modify: `src/MissionControl.Web/wwwroot/app.css:611-631`

**Interfaces:**
- Consumes: `RadarSweep.Current`, `Grid.LoadAsync`, `Conjunction.Path`, `Conjunction.Predict` (Task 1).

- [ ] **Step 1: Étape de mission**

`Mission.cs` : `new(4, "debris", "Interlude", "Débris en orbite", "debris-scan.cs", ["#:include", "StringStream", "Indexeur d'extension", "break outer"])`.

- [ ] **Step 2: Page**

`@page "/debris"`. Supprimer le parsing de `tools/SectorMap.cs` : charger la grille avec `Grid.LoadAsync(new StringStream(RadarSweep.Current, …))`. Une phrase de scénario en haut (image radar centrée sur la sonde, cases de 1 km ; l'outil prédit la trajectoire de chaque débris et déclenche une manœuvre au premier qui passe à 1 km ou moins). Balayage animé comme aujourd'hui, mais sur chaque `#` : appeler `Conjunction.Predict`, ajouter le débris à la liste des évalués, marquer les cases de `Conjunction.Path(pos)` qui sont dans la grille en `path-safe` ou `path-threat`, s'arrêter au premier menaçant. Statut : « Conjonction en (r, c) dans n min ». Légende : sonde, débris, trajectoire sûre, trajectoire menaçante. Axes : libellés en km relatifs à la sonde (−3 … +3, −5 … +5). Panneau « Exécution réelle » : `debris-scan.cs`. `CodeCompare` : quatre fichiers (`debris-scan.cs` avec marques `#:include`, `StringStream`, `grid[pos]`, `break outer`, `outer:` ; `RadarSweep.cs` ; `Grid.cs` ; `Conjunction.cs` avec marque `Predict`).

- [ ] **Step 3: CSS et Préparation**

`app.css` : commentaire « Image radar », `.cell.rock` → `.cell.debris`, ajouter `.cell.path-safe` (vert discret, `var(--green)`) et `.cell.path-threat` (rouge, `var(--red)`). `Preparation.razor` : `asteroid-scan` → `debris-scan` aux quatre endroits.

- [ ] **Step 4: Build**

Run: `dotnet build src/MissionControl.Web`
Expected: build OK, sans nouvel avertissement.

- [ ] **Step 5: Vérifier dans le navigateur**

Lancer l'AppHost (`dotnet run --project src/MissionControl.AppHost --launch-profile https`), ouvrir la console sur `/debris`. Attendu : « Balayer le secteur » trace trois trajectoires vertes, puis une rouge sur `(4, 10)`, sans rien dessiner hors de la grille ; `(6, 3)` reste sans trajectoire ; « Lancer debris-scan.cs » affiche les quatre lignes et le code de sortie 0. La page Préparation liste `debris-scan`. Le lien de l'étape 4 dans la navigation mène à `/debris`.

- [ ] **Step 6: Commit**

`feat(console): page Débris en orbite, trajectoires prévues sur l'image radar`

### Final: vérification d'ensemble

- [ ] Run: `dotnet test` à la racine → tout au vert.
- [ ] `grep -rni "asteroid\|astéro\|SectorMap\|Kuiper" --exclude-dir=bin --exclude-dir=obj --exclude-dir=superpowers .` → aucun résultat (seuls `docs/superpowers/` en gardent la trace, historique).
