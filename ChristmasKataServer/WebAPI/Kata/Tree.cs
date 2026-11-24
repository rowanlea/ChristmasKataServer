namespace WebAPI.Kata;

public sealed class Tree
{
    private readonly Bulb[] _bulbs;

    public Tree(int size)
    {
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
        _bulbs = new Bulb[size * (size + 1) / 2];
    }

    public int Size { get; }

    public int Count => _bulbs.Length;

    public Bulb this[int r, int p]
    {
        get => _bulbs[Locate(r, p)];
        set => _bulbs[Locate(r, p)] = value;
    }

    public static int Index(int r, int p) => r * (r + 1) / 2 + p;

    public bool Contains(int r, int p) => r >= 0 && r < Size && p >= 0 && p <= r;

    public Tree Clone()
    {
        var copy = new Tree(Size);
        _bulbs.CopyTo(copy._bulbs, 0);
        return copy;
    }

    public string ToAnswerLine() => string.Create(_bulbs.Length, _bulbs, static (span, bulbs) =>
    {
        for (var i = 0; i < bulbs.Length; i++) span[i] = bulbs[i].ToChar();
    });

    public static Tree FromAnswerLine(string line, int size)
    {
        var tree = new Tree(size);
        if (line.Length != tree.Count)
            throw new FormatException($"Expected {tree.Count} bulbs for size {size}, got {line.Length}");
        for (var i = 0; i < line.Length; i++) tree._bulbs[i] = BulbChars.Parse(line[i]);
        return tree;
    }

    private int Locate(int r, int p)
    {
        if (!Contains(r, p)) throw new ArgumentOutOfRangeException(nameof(p), $"No bulb at row {r}, position {p}");
        return Index(r, p);
    }
}
