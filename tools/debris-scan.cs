#!/usr/bin/env dotnet
#:include RadarSweep.cs
#:include Grid.cs
#:include Conjunction.cs

using System.Text;

Console.OutputEncoding = Encoding.UTF8;

using var sweep = new StringStream(RadarSweep.Current, Encoding.UTF8);
var grid = await Grid.LoadAsync(sweep);

// La sonde « P » est au centre de l'image radar.
Position probe = default;
for (var row = 0; row < grid.Count; row++)
{
    var col = grid[row].IndexOf('P');
    if (col >= 0) probe = new Position(row, col);
}

var threat = false;
outer:
for (var row = 0; row < grid.Count; row++)
{
    for (var col = 0; col < grid[row].Length; col++)
    {
        var pos = new Position(row, col);
        if (grid[pos] != '#') continue;

        var minutes = Conjunction.Predict(pos, probe);
        if (minutes is null)
        {
            Console.WriteLine($"Débris en ({pos.Row}, {pos.Col}) : trajectoire divergente, sans danger.");
            continue;
        }

        Console.WriteLine($"Débris en ({pos.Row}, {pos.Col}) : conjonction dans {minutes} min — manœuvre d'évitement.");
        threat = true;
        break outer;
    }
}

if (!threat) Console.WriteLine("Secteur dégagé.");
