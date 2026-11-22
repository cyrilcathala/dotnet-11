#!/usr/bin/env dotnet
#:include SectorMap.cs
#:include Grid.cs

using System.Text;

Console.OutputEncoding = Encoding.UTF8;

using var map = new StringStream(SectorMap.Kuiper, Encoding.UTF8);
var grid = await Grid.LoadAsync(map);

Position? hit = null;
outer:
for (var row = 0; row < grid.Count; row++)
{
    for (var col = 0; col < grid[row].Length; col++)
    {
        var pos = new Position(row, col);
        if (grid[pos] == '#')
        {
            hit = pos;
            break outer;
        }
    }
}

Console.WriteLine(hit is { } p
    ? $"Astéroïde détecté en ({p.Row}, {p.Col}) — trajectoire corrigée."
    : "Secteur dégagé.");
