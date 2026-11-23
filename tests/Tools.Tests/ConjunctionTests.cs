namespace Tools.Tests;

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
        => Assert.Equal(new Position[] { new(4, 9), new(4, 8), new(4, 7), new(4, 6), new(4, 5) }, Conjunction.Path(new(4, 10)));

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
