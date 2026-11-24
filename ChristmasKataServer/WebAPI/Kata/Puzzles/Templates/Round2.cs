namespace WebAPI.Kata.Puzzles.Templates;

internal static class Round2
{
    private static ScriptBuilder Start(Random rng)
    {
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.Pattern(b, rng, rng.Next(20, 31));
        return b;
    }

    public static string P21(Random rng) => Start(rng).Line("ROTATE").Line("MIRROR").ToString();

    public static string P22(Random rng) => Start(rng).Line("MIRROR").Line("ROTATE").ToString();

    public static string P23(Random rng)
    {
        var b = Start(rng);
        bool[] rotates;
        do rotates = Enumerable.Range(0, 30).Select(_ => rng.Next(2) == 0).ToArray();
        while (!Distinguishable(rotates));
        foreach (var rotate in rotates) b.Line(rotate ? "ROTATE" : "MIRROR");
        return b.ToString();
    }

    /// <summary>
    /// True when a wrong rotation and a wrong mirror axis each move the three corners somewhere else, which
    /// is enough to tell the two transforms apart on a dense pattern.
    /// </summary>
    private static bool Distinguishable(bool[] rotates)
    {
        var right = Corners(rotates, Geometry.Rotate, (_, r, p) => Geometry.Mirror(r, p));
        return !right.SequenceEqual(Corners(rotates, Geometry.RotateCounterClockwise, (_, r, p) => Geometry.Mirror(r, p)))
            && !right.SequenceEqual(Corners(rotates, Geometry.Rotate, Geometry.MirrorWrongAxis));
    }

    private static (int Row, int Position)[] Corners(
        bool[] rotates, Func<int, int, int, (int r, int p)> rotate, Func<int, int, int, (int r, int p)> mirror) =>
        Paint.Corners.Select(corner => rotates.Aggregate(corner,
            (at, isRotate) => (isRotate ? rotate : mirror)(Paint.Size, at.Row, at.Position))).ToArray();

    public static string P24(Random rng)
    {
        var first = Paint.Colour(rng);
        var second = Paint.ColourOtherThan(rng, [first]);
        var third = Paint.ColourOtherThan(rng, [first, second]);
        var b = new ScriptBuilder().Size(Paint.Size);
        Paint.Pattern(b, rng, 14);
        b.Line($"ROW 15 {first}").Line("ROTATE")
            .Line($"ROW 15 {second}");
        Sets(b, rng);
        b.Line("ROTATE").Line($"ROW 15 {third}");
        if (rng.Next(2) == 0) b.Line("MIRROR");
        Sets(b, rng);
        return b.ToString();
    }

    private static void Sets(ScriptBuilder b, Random rng)
    {
        for (var i = rng.Next(1, 5); i > 0; i--) Paint.SetLine(b, rng);
    }
}
