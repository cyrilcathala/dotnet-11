# Étape 4 : de `asteroid-scan` à `debris-scan`

## Pourquoi

L'interlude de l'étape 4 n'a pas d'objectif lisible : le script s'arrête au premier `#` de la carte et annonce « trajectoire corrigée » sans rien corriger, la sonde `P` n'est jamais lue. On garde les quatre nouveautés montrées (`#:include`, `StringStream`, indexeur d'extension `grid[pos]`, `break outer`) et on leur donne un scénario crédible : **l'évitement d'un débris en orbite**, opération réelle (l'ISS manœuvre plusieurs fois par an).

## Objectifs / non-objectifs

- Objectif : un public comprend en une phrase ce que fait l'outil et pourquoi il s'arrête.
- Objectif : les quatre nouveautés restent visibles, plus un troisième `#:include`.
- Non-objectif : un vrai calcul orbital. La prédiction est volontairement simulée.
- Anti-objectif : une logique dupliquée entre le script et la page qui finirait par diverger.

## Approches considérées

- **A. Table des vitesses dans `Conjunction.cs` (retenue).** La carte garde des `#`, une méthode « boîte noire » prédit la trajectoire de chaque écho.
- B. Direction écrite dans la carte (`>`, `v`…) : visuel, mais la méthode n'a plus rien à cacher et `grid[pos] == '#'` devient un test sur quatre caractères.
- C. Dérive commune à tous les débris : trajectoires parallèles, peu crédible.

## Scénario

- La grille est une **image radar centrée sur la sonde** : chaque case couvre 1 km × 1 km, `P` est au centre, chaque `#` est un écho (un débris détecté). Un commentaire dans le fichier de carte le dit.
- Le script repère `P`, puis balaye l'image ligne par ligne. Pour chaque écho, il demande à `Conjunction` sa trajectoire prévue.
- Écho sans danger : une ligne « trajectoire divergente, sans danger », on continue.
- Premier écho menaçant : une ligne « conjonction dans N min — manœuvre d'évitement », puis `break outer;`. Les échos suivants ne sont pas évalués.
- Aucun écho menaçant : « Secteur dégagé. »
- La carte est choisie pour que **au moins deux échos soient jugés sans danger avant le menaçant**, et qu'il reste au moins un écho après lui (non évalué), pour que `break outer` se voie.

## Code

- `tools/debris-scan.cs` (renommé) : `#:include RadarSweep.cs`, `#:include Grid.cs`, `#:include Conjunction.cs` ; lecture par `StringStream` ; boucles imbriquées avec `grid[pos] == '#'` puis `Conjunction.Predict(pos, probe)` ; `break outer;`.
- `tools/RadarSweep.cs` (remplace `SectorMap.cs`) : chaîne brute `RadarSweep.Current`.
- `tools/Grid.cs` : inchangé.
- `tools/Conjunction.cs` (nouveau) : une table `Position → vitesse (lignes, colonnes par minute)` ; `Predict` avance la position minute par minute sur un horizon court et renvoie la minute où le débris passe à une case ou moins de la sonde, ou `null`. Un écho absent de la table est immobile.
- Test `AsteroidScanTests` renommé `DebrisScanTests` : vérifie le code de sortie 0, la ligne de manœuvre attendue, et qu'un écho situé après le menaçant n'apparaît pas dans la sortie.

## Page de la console (`/debris`)

- Route `/debris`, étape 4 renommée « Débris en orbite » dans `Mission.cs`, nouveautés inchangées.
- La grille garde son animation de balayage. Chaque écho évalué affiche sa trajectoire prévue : vert s'il diverge, rouge s'il menace la sonde. Légende : sonde, écho, trajectoire sûre, trajectoire menaçante. Une phrase en haut explique le scénario.
- **Mon choix** : la page compile `Grid.cs`, `Conjunction.cs` et `RadarSweep.cs` par lien (`<Compile Include="../../tools/…" Link="…" />` dans `MissionControl.Web.csproj`) au lieu de reparser la source et de recopier le parcours. Une seule source de vérité pour la carte et les vitesses.
- Panneau « Exécution réelle » et comparaison de code : mêmes rôles, fichiers mis à jour (quatre fichiers montrés).

## Renommage

`asteroid-scan` devient `debris-scan` partout : `AppHost.cs`, `Preparation.razor`, `Mission.cs`, `app.css` (commentaire, classes si besoin), `docs/demo-runbook.md`, `docs/demo.http`, `docs/architecture.mmd`. Les specs et plans datés du 2026-11-22 restent tels quels (historique).

## Laissé au développeur

- Les dimensions exactes de l'image radar et la position des échos, dans le respect de la contrainte ci-dessus.
- Le texte exact des lignes de sortie, en français, dans le ton des autres outils.
