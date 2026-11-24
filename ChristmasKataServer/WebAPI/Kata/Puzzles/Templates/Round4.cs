namespace WebAPI.Kata.Puzzles.Templates;

internal static class Round4
{
    public static string P41(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        foreach (var cell in Paint.DistinctCells(rng, rng.Next(3, 7))) Paint.Set(b, cell, Paint.Colour(rng));
        var colour = Paint.Colour(rng);
        for (var run = rng.Next(3, 6); run > 0; run--)
        {
            for (var i = rng.Next(8, 11); i > 0; i--) b.Line($"SEQUENCE {colour}");
            colour = Paint.ColourOtherThan(rng, [colour]);
        }
        return b.ToString();
    }

    public static string P42(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.DenseSets(b, rng);
        int laps;
        do laps = rng.Next(20, 201);
        while (laps % 136 == 0);
        return b.Repeat(laps, body => body.Line("CHASE")).ToString();
    }

    public static string P43(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        for (var i = rng.Next(3, 7); i > 0; i--) Paint.RowLine(b, rng);
        for (var block = rng.Next(2, 4); block > 0; block--)
        {
            b.Repeat(rng.Next(2, 7), body => body.Line("FALL"));
            for (var i = rng.Next(1, 4); i > 0; i--) Paint.SetLine(b, rng);
        }
        return b.ToString();
    }

    public static string P44(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.DenseSets(b, rng);
        var steps = new string[rng.Next(6, 11)];
        for (var i = 0; i < steps.Length; i++) steps[i] = RandomStep(rng, steps[..i]);
        var rotate = rng.Next(steps.Length - 1);
        steps[rotate] = "ROTATE";
        steps[rng.Next(rotate + 1, steps.Length)] = ChaseBlock(rng);
        foreach (var step in steps) b.Line(step);
        return b.ToString();
    }

    private static string ChaseBlock(Random rng) => $"REPEAT {rng.Next(20, 121)}\nCHASE\nEND";

    private static string RandomStep(Random rng, string[] before)
    {
        var step = rng.Next(5) switch
        {
            0 => $"SEQUENCE {Paint.Colour(rng)}",
            1 => ChaseBlock(rng),
            2 => "FALL",
            3 => "ROTATE",
            _ => "MIRROR",
        };
        return step == "FALL" && before.Count(s => s == "FALL") >= 2 ? ChaseBlock(rng) : step;
    }
}
