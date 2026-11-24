namespace WebAPI.Kata.Puzzles.Templates;

internal static class Paint
{
    private const string Colours = "RGBW";
    public const int Size = 16;

    public static readonly (int Row, int Position)[] Corners = [(0, 0), (Size - 1, 0), (Size - 1, Size - 1)];

    public static char Colour(Random rng) => Colours[rng.Next(Colours.Length)];

    public static char ColourOtherThan(Random rng, IReadOnlyCollection<char> taken)
    {
        char c;
        do c = Colour(rng);
        while (taken.Contains(c));
        return c;
    }

    public static (int Row, int Position) Cell(Random rng)
    {
        var row = rng.Next(Size);
        return (row, rng.Next(row + 1));
    }

    public static List<(int Row, int Position)> DistinctCells(
        Random rng, int count, IEnumerable<(int Row, int Position)>? taken = null)
    {
        var used = new HashSet<(int, int)>(taken ?? []);
        var cells = new List<(int Row, int Position)>();
        while (cells.Count < count)
        {
            var cell = Cell(rng);
            if (used.Add(cell)) cells.Add(cell);
        }
        return cells;
    }

    public static void Set(ScriptBuilder b, (int Row, int Position) cell, char colour) =>
        b.Line($"SET {cell.Row} {cell.Position} {colour}");

    public static void RowLine(ScriptBuilder b, Random rng) => b.Line($"ROW {rng.Next(4, Size)} {Colour(rng)}");

    public static void SetLine(ScriptBuilder b, Random rng) => Set(b, Cell(rng), Colour(rng));

    /// <summary>Dense, asymmetric painting: ROW and SET lines in random order.</summary>
    public static void Pattern(ScriptBuilder b, Random rng, int lines)
    {
        for (var i = 0; i < lines; i++)
        {
            if (rng.Next(100) < 40) RowLine(b, rng);
            else SetLine(b, rng);
        }
    }

    /// <summary>Random colours on about 120 distinct bulbs: nearly every bulb lit, no uniform stripes.</summary>
    public static void DenseSets(ScriptBuilder b, Random rng)
    {
        foreach (var cell in DistinctCells(rng, rng.Next(115, 126))) Set(b, cell, Colour(rng));
    }

    /// <summary>Many random single-bulb SETs: dense and colour-varied inside every row.</summary>
    public static void ScatteredSets(ScriptBuilder b, Random rng, int lines)
    {
        for (var i = 0; i < lines; i++) SetLine(b, rng);
    }

    public static void Shuffle<T>(Random rng, IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
