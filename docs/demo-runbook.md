# Runbook de démo

## Mesures

Nombre de frames de `GET /diagnostics/countdown-stack` (LaunchControl, .NET 11.0.100, build Debug, `dotnet LaunchControl.dll` puis `curl`) :

| Configuration | Frames |
|---|---|
| Sans runtime async (`runtime-async=off`) | 14 |
| Avec `<Features>runtime-async=on</Features>` | 7 |

Frames = lignes de la pile commençant par `   at `. Mesuré en retirant puis remettant la propriété dans `LaunchControl.csproj`. Référence .NET 10 (spec, autre chaîne d appels) : 13 frames ; `compare/net10` (Task 12) le re-mesurera.
