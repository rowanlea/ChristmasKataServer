namespace WebAPI.Kata.Puzzles.Templates;

internal static class Round6
{
    private const long MinCount = 999_000_000;
    private const long MaxCount = 1_000_000_000;
    private const int MinSeedSpacing = 7;
    private const int OuterPasses = 25;

    private static long BigCount(Random rng) => rng.NextInt64(MinCount, MaxCount + 1);

    public static string P61(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.DenseSets(b, rng);
        b.Repeat(BigCount(rng), body => Lines(body, rng, "ROTATE", "CYCLE"));
        return b.ToString();
    }

    /// <summary>Few, widely spaced seeds and a GROW in the loop, so the tree takes a while to fill before it settles.</summary>
    public static string P62(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        var seeds = Seeds(b, rng, rng.Next(2, 4), MinSeedSpacing);
        var growColour = seeds[rng.Next(seeds.Count)];
        b.Repeat(BigCount(rng), body => Lines(body, rng, "ROTATE", "CYCLE", $"GROW {growColour}"));
        return b.ToString();
    }

    public static string P63(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.DenseSets(b, rng);
        b.Repeat(OuterPasses, outer =>
        {
            Paint.SetLine(outer, rng);
            outer.Repeat(BigCount(rng), inner => Lines(inner, rng, "ROTATE", "CHASE", "CYCLE"));
        });
        return b.ToString();
    }

    /// <summary>Emits SET lines on distinct cells at least <c>spacing</c> apart; returns their colours.</summary>
    private static List<char> Seeds(ScriptBuilder b, Random rng, int count, int spacing)
    {
        var cells = new List<(int Row, int Position)>();
        var colours = new List<char>();
        while (cells.Count < count)
        {
            var cell = Paint.Cell(rng);
            if (cells.Any(c => Math.Abs(c.Row - cell.Row) + Math.Abs(c.Position - cell.Position) < spacing))
                continue;
            cells.Add(cell);
            colours.Add(Paint.Colour(rng));
            Paint.Set(b, cell, colours[^1]);
        }
        return colours;
    }

    private static void Lines(ScriptBuilder b, Random rng, params string[] lines)
    {
        var order = lines.ToList();
        Paint.Shuffle(rng, order);
        foreach (var line in order) b.Line(line);
    }
}
