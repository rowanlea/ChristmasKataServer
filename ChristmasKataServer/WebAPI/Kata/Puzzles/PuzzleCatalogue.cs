namespace WebAPI.Kata.Puzzles;

public static class PuzzleCatalogue
{
    private static Puzzle Make(string id, string name)
    {
        var round = int.Parse(id[..id.IndexOf('.')]);
        return new Puzzle(id, name, round, id == "7.1" ? 48 : 16,
            _ => throw new NotImplementedException($"Template {id} is not written yet"));
    }

    public static IReadOnlyList<Puzzle> All { get; } =
    [
        Make("1.1", "Paint by Numbers"),
        Make("1.2", "Fill Your Boots"),
        Make("1.3", "Turn of Events"),
        Make("1.4", "Looking Glass"),
        Make("2.1", "Turn, Then Reflect"),
        Make("2.2", "Second Thoughts"),
        Make("2.3", "Round in Circles"),
        Make("2.4", "Three Sides to Every Story"),
        Make("3.1", "Growing Pains"),
        Make("3.2", "Turf War"),
        Make("3.3", "Cornered"),
        Make("3.4", "Change of Heart"),
        Make("4.1", "Switched On"),
        Make("4.2", "Chasing Tail"),
        Make("4.3", "Falling Star"),
        Make("4.4", "Light Show"),
        Make("5.1", "The Long List"),
        Make("5.2", "Wheels Within Wheels"),
        Make("5.3", "Nothing Doing"),
        Make("6.1", "Round the Clock"),
        Make("6.2", "False Start"),
        Make("6.3", "Deep Sleep"),
        Make("7.1", "Secret Santa"),
    ];

    public static Puzzle? Find(string id) => All.FirstOrDefault(p => p.Id == id);

    public static Puzzle? Previous(Puzzle p)
    {
        var i = IndexOf(p);
        return i > 0 ? All[i - 1] : null;
    }

    public static Puzzle? Next(Puzzle p)
    {
        var i = IndexOf(p);
        return i >= 0 && i < All.Count - 1 ? All[i + 1] : null;
    }

    public static IEnumerable<Puzzle> InRound(int round) => All.Where(p => p.Round == round);

    private static int IndexOf(Puzzle p)
    {
        for (var i = 0; i < All.Count; i++)
            if (All[i].Id == p.Id) return i;
        return -1;
    }
}
