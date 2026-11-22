public record struct Position(int Row, int Col);

public static class Grid
{
    public static async Task<IReadOnlyList<string>> LoadAsync(Stream source)
    {
        using var reader = new StreamReader(source, leaveOpen: true);
        var rows = new List<string>();
        while (await reader.ReadLineAsync() is { } line)
            if (line.Length > 0) rows.Add(line);
        return rows;
    }
}

public static class GridExtensions
{
    extension(IReadOnlyList<string> rows)
    {
        public char this[Position p] => rows[p.Row][p.Col];
    }
}
