// Station météo de pré-lancement. Variables lues : LAUNCH_SITE, PROBE, FORCE_STORM.
// Sortie : rapport sur 5 lignes ; code de sortie 0 = GO, 1 = NO-GO.
Console.OutputEncoding = System.Text.Encoding.UTF8;

var site = Environment.GetEnvironmentVariable("LAUNCH_SITE") ?? "inconnu";
var probe = Environment.GetEnvironmentVariable("PROBE") ?? "inconnue";
var storm = string.Equals(Environment.GetEnvironmentVariable("FORCE_STORM"), "true", StringComparison.OrdinalIgnoreCase);

Console.WriteLine($"Station météo — {site} — sonde {probe}");
Console.WriteLine("Vent : 12 km/h");
Console.WriteLine(storm ? "Orages : cellule orageuse à 8 km" : "Orages : aucun");
Console.WriteLine(storm ? "Couverture nuageuse : 90 %" : "Couverture nuageuse : 20 %");
Console.WriteLine(storm ? "NO-GO" : "GO");
return storm ? 1 : 0;
