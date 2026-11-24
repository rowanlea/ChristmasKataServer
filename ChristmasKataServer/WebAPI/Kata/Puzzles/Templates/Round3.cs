namespace WebAPI.Kata.Puzzles.Templates;

internal static class Round3
{
    public static string P31(Random rng)
    {
        var colour = Paint.Colour(rng);
        var b = new ScriptBuilder().Size(Paint.Size);
        foreach (var cell in Paint.DistinctCells(rng, rng.Next(2, 5))) Paint.Set(b, cell, colour);
        for (var i = rng.Next(1, 7); i > 0; i--) b.Line($"GROW {colour}");
        return b.ToString();
    }

    public static string P32(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        var cells = Paint.DistinctCells(rng, rng.Next(4, 7));
        for (var i = 0; i < cells.Count; i++) Paint.Set(b, cells[i], i % 2 == 0 ? 'R' : 'G');
        var total = rng.Next(4, 9);
        for (var i = 0; i < total; i++) b.Line(i % 2 == 0 ? "GROW R" : "GROW G");
        return b.ToString();
    }

    public static string P33(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        var corners = Paint.Corners.ToList();
        Paint.Shuffle(rng, corners);
        var seeded = corners.Take(rng.Next(2, 4)).ToList();
        var colours = new List<char>();
        foreach (var corner in seeded)
        {
            colours.Add(Paint.ColourOtherThan(rng, colours));
            Paint.Set(b, corner, colours[^1]);
        }
        foreach (var cell in Paint.DistinctCells(rng, rng.Next(3, 7), seeded))
            Paint.Set(b, cell, colours[rng.Next(colours.Count)]);

        int steps;
        do steps = rng.Next(4, 9);
        while (steps % 3 == 0);
        for (var i = steps; i > 0; i--)
        {
            b.Line($"GROW {colours[rng.Next(colours.Count)]}");
            b.Line("ROTATE");
        }
        return b.ToString();
    }

    public static string P34(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        var corner = Paint.Corners[rng.Next(Paint.Corners.Length)];
        Paint.Set(b, corner, Paint.Colour(rng));
        foreach (var cell in Paint.DistinctCells(rng, rng.Next(1, 4), [corner])) Paint.Set(b, cell, Paint.Colour(rng));
        var rotate = rng.Next(2) == 0;
        int count;
        do count = rng.Next(rotate ? 7 : 4, 13);
        while (rotate && count % 3 == 0);
        b.Repeat(count, body =>
        {
            body.Line("CYCLE").Line("GROW R");
            if (rotate) body.Line("ROTATE");
        });
        return b.ToString();
    }
}
