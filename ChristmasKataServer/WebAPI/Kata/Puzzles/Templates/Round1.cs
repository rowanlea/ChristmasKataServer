namespace WebAPI.Kata.Puzzles.Templates;

internal static class Round1
{
    public static string P11(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        foreach (var cell in Paint.DistinctCells(rng, 30)) Paint.Set(b, cell, Paint.Colour(rng));
        return b.ToString();
    }

    public static string P12(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size).Line($"FILL {Paint.Colour(rng)}");
        var kinds = Enumerable.Repeat(true, rng.Next(4, 9)).Concat(Enumerable.Repeat(false, rng.Next(10, 21))).ToList();
        Paint.Shuffle(rng, kinds);
        foreach (var isRow in kinds)
        {
            if (isRow) Paint.RowLine(b, rng);
            else Paint.SetLine(b, rng);
        }
        return b.ToString();
    }

    public static string P13(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.Pattern(b, rng, 20);
        return b.Line("ROTATE").ToString();
    }

    public static string P14(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.Pattern(b, rng, 16);
        for (var i = rng.Next(3, 6); i > 0; i--)
        {
            var row = 2 * rng.Next(1, Paint.Size / 2);
            Paint.Set(b, (row, row / 2), Paint.Colour(rng));
        }
        return b.Line("MIRROR").ToString();
    }
}
