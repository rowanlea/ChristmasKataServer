namespace WebAPI.Kata.Puzzles.Templates;

internal static class Round5
{
    private const int LongListLines = 5000;
    private const int FinishingSets = 78;

    public static string P51(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        for (var i = 0; i < LongListLines - FinishingSets - 2; i++) b.Line(LongListLine(rng));
        Finish(b, rng, FinishingSets);
        return b.ToString();
    }

    /// <summary>Closes on fresh SET lines and one ROTATE and MIRROR, so a wrong transform can never go unnoticed.</summary>
    private static void Finish(ScriptBuilder b, Random rng, int sets)
    {
        for (var i = 0; i < sets; i++) Paint.SetLine(b, rng);
        b.Line("ROTATE").Line("MIRROR");
    }

    private static string LongListLine(Random rng)
    {
        var roll = rng.Next(100);
        if (roll < 2) return $"FILL {Paint.Colour(rng)}";
        if (roll < 4) return $"ROW {rng.Next(Paint.Size)} {Paint.Colour(rng)}";
        return roll switch
        {
            < 40 => SetText(rng),
            < 50 => "ROTATE",
            < 58 => "MIRROR",
            < 68 => $"GROW {Paint.Colour(rng)}",
            < 74 => "CYCLE",
            < 82 => $"SEQUENCE {Paint.Colour(rng)}",
            < 90 => "CHASE",
            _ => "FALL",
        };
    }

    private static string SetText(Random rng)
    {
        var (row, position) = Paint.Cell(rng);
        return $"SET {row} {position} {Paint.Colour(rng)}";
    }

    private const long MaxWork = 100_000;

    public static string P52(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.Pattern(b, rng, rng.Next(14, 21));
        var counts = NestedCounts(rng);
        Nest(b, rng, counts, 0);
        Finish(b, rng, rng.Next(60, 91));
        return b.ToString();
    }

    private static int[] NestedCounts(Random rng)
    {
        var counts = new int[4];
        do
        {
            for (var i = 0; i < counts.Length; i++) counts[i] = rng.Next(1, 21);
        } while (counts.Aggregate(1L, (a, c) => a * c) * 8 > MaxWork);
        return counts;
    }

    private static void Nest(ScriptBuilder b, Random rng, int[] counts, int depth)
    {
        b.Repeat(counts[depth], body =>
        {
            Body(body, rng, 1, 2);
            if (depth + 1 < counts.Length) Nest(body, rng, counts, depth + 1);
            Body(body, rng, 0, 2);
        });
    }

    private static void Body(ScriptBuilder b, Random rng, int min, int max)
    {
        for (var i = rng.Next(min, max + 1); i > 0; i--)
        {
            b.Line(rng.Next(9) switch
            {
                0 => SetText(rng),
                1 => $"ROW {rng.Next(Paint.Size)} {Paint.Colour(rng)}",
                2 => "ROTATE",
                3 => "MIRROR",
                4 => $"GROW {Paint.Colour(rng)}",
                5 => "CYCLE",
                6 => $"SEQUENCE {Paint.Colour(rng)}",
                7 => "CHASE",
                _ => "FALL",
            });
        }
    }

    public static string P53(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.Pattern(b, rng, rng.Next(20, 31));
        b.Line("# the next block never runs")
            .Repeat(0, body =>
            {
                body.Line("# nothing to see here").Line("");
                body.Line($"FILL {Paint.Colour(rng)}");
                Paint.RowLine(body, rng);
                Paint.SetLine(body, rng);
            });
        b.Line("");
        b.Repeat(1, body =>
        {
            body.Line("# once is enough");
            Paint.SetLine(body, rng);
            body.Line("ROTATE");
            body.Line("");
        });
        b.Repeat(rng.Next(2, 100), _ => { });
        b.Repeat(rng.Next(2, 9), body => body.Line("# empty on purpose"));
        for (var i = rng.Next(1, 4); i > 0; i--) Paint.SetLine(b, rng);
        return b.ToString();
    }
}
