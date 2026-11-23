// Prédiction de conjonction volontairement simulée : pas de calcul orbital,
// une table de vitesses (lignes, colonnes par minute) tient lieu de suivi radar.
public static class Conjunction
{
    public const int HorizonMinutes = 5;

    // Un écho absent de la table est considéré immobile.
    private static readonly Dictionary<Position, (int Rows, int Cols)> Velocities = new()
    {
        [new(0, 2)] = (0, 1),
        [new(1, 8)] = (0, 1),
        [new(2, 1)] = (1, 1),
        [new(4, 10)] = (0, -1),
        [new(6, 3)] = (1, 0),
    };

    // Positions prévues pour t = 1 … HorizonMinutes, sans bornes de grille.
    public static IReadOnlyList<Position> Path(Position debris)
    {
        var (rows, cols) = Velocities.GetValueOrDefault(debris);
        return [.. Enumerable.Range(1, HorizonMinutes)
            .Select(t => new Position(debris.Row + t * rows, debris.Col + t * cols))];
    }

    // Minute de la première conjonction (à une case ou moins de la sonde), sinon null.
    public static int? Predict(Position debris, Position probe)
    {
        var path = Path(debris);
        for (var i = 0; i < path.Count; i++)
            if (Math.Max(Math.Abs(path[i].Row - probe.Row), Math.Abs(path[i].Col - probe.Col)) <= 1)
                return i + 1;
        return null;
    }
}
