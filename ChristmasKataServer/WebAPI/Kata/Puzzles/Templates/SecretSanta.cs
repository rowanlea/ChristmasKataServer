namespace WebAPI.Kata.Puzzles.Templates;

public static class SecretSanta
{
    public const int Size = 48;
    private const int ArtTop = 34;
    private const int SnowRows = 34;
    private const int SnowPercent = 8;
    private const int MaxOffset = 3;
    private const int OrbitCap = 20_000;
    private const long MinCount = 999_000_000;
    private const long MaxCount = 1_000_000_000;
    private static readonly char[] SackColours = ['R', 'B', 'W'];
    private static readonly string[] BodyOps = ["ROTATE", "MIRROR", "CHASE", "CYCLE"];

    public sealed record Scene(bool FacingLeft, int Offset, char Sack);

    public static Scene Layout(Random rng) => new(
        rng.Next(2) == 0,
        rng.Next(-MaxOffset, MaxOffset + 1),
        SackColours[rng.Next(SackColours.Length)]);

    public static IReadOnlyList<string> ArtLines()
    {
        using var stream = typeof(SecretSanta).Assembly.GetManifestResourceStream("secret-santa.txt")
            ?? throw new InvalidOperationException("secret-santa.txt is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
    }

    public static Tree Picture(Random rng)
    {
        var scene = Layout(rng);
        var tree = new Tree(Size);
        for (var r = 0; r < Size; r++)
            for (var p = 0; p <= r; p++)
                tree[r, p] = r < SnowRows && rng.Next(100) < SnowPercent ? Bulb.W : Bulb.G;

        var art = ArtLines();
        for (var i = 0; i < art.Count; i++)
        {
            var r = ArtTop + i;
            var line = scene.FacingLeft ? new string(art[i].Reverse().ToArray()) : art[i];
            var start = (r + 1 - (ArtTop + 1)) / 2 + scene.Offset;
            for (var k = 0; k < line.Length; k++)
            {
                var p = start + k;
                if (line[k] == '.' || p < 0 || p > r) continue;
                tree[r, p] = BulbChars.Parse(line[k] == 'S' ? scene.Sack : line[k]);
            }
        }
        return tree;
    }

    public static string Template(Random rng)
    {
        var picture = Picture(rng);
        while (true)
        {
            var l2 = Body(rng);
            var l1 = Body(rng);
            var c2 = rng.NextInt64(MinCount, MaxCount + 1);
            var c1 = rng.NextInt64(MinCount, MaxCount + 1);

            var beforeSecond = Unwind(picture, l2, c2);
            if (beforeSecond is null) continue;
            var start = Unwind(beforeSecond, l1, c1);
            if (start is null) continue;

            return Script(start, l1, c1, l2, c2);
        }
    }

    public static int OrbitPeriod(Tree start, IReadOnlyList<Instruction> body, int cap)
    {
        var options = new EmulatorOptions(InstructionBudget: ((long)cap + 1) * (body.Count + 1) + 10);
        var script = new Script(start.Size, [new Repeat((long)cap + 1, body)]);
        var result = new Emulator(options).Run(script, start);
        return result.Loops[0].Period is { } period ? period : -1;
    }

    /// <summary>The tree that the body, run count times, turns into the target. Null when the orbit is too long or a power of two.</summary>
    private static Tree? Unwind(Tree target, IReadOnlyList<Instruction> body, long count)
    {
        var period = OrbitPeriod(target, body, OrbitCap);
        if (period < 0 || (period & (period - 1)) == 0) return null;
        var back = (int)((period - count % period) % period);
        var script = new Script(target.Size, [new Repeat(back, body)]);
        return new Emulator(new EmulatorOptions(DetectCycles: false)).Run(script, target).Final;
    }

    private static IReadOnlyList<Instruction> Body(Random rng)
    {
        var ops = new string[rng.Next(2, 5)];
        for (var i = 0; i < ops.Length; i++) ops[i] = BodyOps[rng.Next(BodyOps.Length)];
        if (!ops.Contains("CHASE")) ops[rng.Next(ops.Length)] = "CHASE";
        return ops.Select(ToInstruction).ToList();
    }

    private static Instruction ToInstruction(string op) => op switch
    {
        "ROTATE" => new Rotate(),
        "MIRROR" => new Mirror(),
        "CHASE" => new Chase(),
        _ => new Cycle(),
    };

    private static string Name(Instruction i) => i switch
    {
        Rotate => "ROTATE",
        Mirror => "MIRROR",
        Chase => "CHASE",
        _ => "CYCLE",
    };

    private static string Script(Tree start, IReadOnlyList<Instruction> l1, long c1, IReadOnlyList<Instruction> l2, long c2)
    {
        var b = new ScriptBuilder().Size(Size).Line("FILL G");
        for (var r = 0; r < Size; r++)
            for (var p = 0; p <= r; p++)
                if (start[r, p] != Bulb.G) b.Line($"SET {r} {p} {start[r, p].ToChar()}");
        b.Repeat(c1, body => { foreach (var i in l1) body.Line(Name(i)); });
        b.Repeat(c2, body => { foreach (var i in l2) body.Line(Name(i)); });
        return b.ToString();
    }
}
