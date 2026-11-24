using WebAPI.Kata;

namespace Tests.Kata;

public class EmulatorTests
{
    private static Tree Run(string body, int size, EmulatorOptions? options = null) =>
        new Emulator(options).Run(ScriptParser.Parse($"SIZE {size}\n{body}")).Final;

    private static string RandomTreeScript(int size, int seed)
    {
        var rng = new Random(seed);
        var lines = new List<string>();
        for (var r = 0; r < size; r++)
            for (var p = 0; p <= r; p++)
                lines.Add($"SET {r} {p} {"KRGBW"[rng.Next(5)]}");
        return string.Join('\n', lines);
    }

    [Test]
    public void Rotate_moves_star_to_bottom_right_corner()
    {
        var tree = Run("SET 0 0 R\nROTATE", 4);
        Assert.That(tree.ToAnswerLine(), Is.EqualTo("KKKKKKKKKR"));
        Assert.That(tree[3, 3], Is.EqualTo(Bulb.R));
    }

    [Test]
    public void Three_rotations_are_identity()
    {
        var setup = RandomTreeScript(16, 1);
        Assert.That(Run(setup + "\nROTATE\nROTATE\nROTATE", 16).ToAnswerLine(),
            Is.EqualTo(Run(setup, 16).ToAnswerLine()));
    }

    [Test]
    public void Mirror_twice_is_identity()
    {
        var setup = RandomTreeScript(16, 2);
        Assert.That(Run(setup + "\nMIRROR\nMIRROR", 16).ToAnswerLine(),
            Is.EqualTo(Run(setup, 16).ToAnswerLine()));
    }

    [Test]
    public void Mirror_rotate_mirror_equals_two_rotations()
    {
        var setup = RandomTreeScript(5, 3);
        Assert.That(Run(setup + "\nMIRROR\nROTATE\nMIRROR", 5).ToAnswerLine(),
            Is.EqualTo(Run(setup + "\nROTATE\nROTATE", 5).ToAnswerLine()));
    }

    [Test]
    public void Mirror_keeps_middle_bulb_of_odd_rows()
    {
        var tree = Run("SET 2 1 R\nSET 2 0 G\nMIRROR", 4);
        Assert.That(tree[2, 1], Is.EqualTo(Bulb.R));
        Assert.That(tree[2, 2], Is.EqualTo(Bulb.G));
        Assert.That(tree[2, 0], Is.EqualTo(Bulb.K));
    }

    [Test]
    public void Grow_updates_simultaneously()
    {
        Assert.That(Run("SET 3 0 R\nGROW R", 4).ToAnswerLine(), Is.EqualTo("KKKRKKRRKK"));
    }

    [Test]
    public void Grow_only_fills_off_bulbs()
    {
        var tree = Run("SET 1 0 R\nSET 1 1 G\nGROW R", 3);
        Assert.That(tree[1, 1], Is.EqualTo(Bulb.G));
    }

    [Test]
    public void Cycle_skips_off_bulbs()
    {
        Assert.That(Run("FILL K\nSET 0 0 W\nCYCLE", 4).ToAnswerLine(), Is.EqualTo("RKKKKKKKKK"));
    }

    [Test]
    public void Counter_clockwise_quirk_differs()
    {
        var tree = Run("SET 0 0 R\nROTATE", 4, new EmulatorOptions(Quirks.CounterClockwiseRotate));
        Assert.That(tree[3, 0], Is.EqualTo(Bulb.R));
    }

    [Test]
    public void Wrong_axis_mirror_quirk_differs()
    {
        var tree = Run("SET 0 0 R\nMIRROR", 4, new EmulatorOptions(Quirks.MirrorWrongAxis));
        Assert.That(tree[3, 3], Is.EqualTo(Bulb.R));
    }

    [Test]
    public void Wiring_snakes_down_the_tree()
    {
        Assert.That(Geometry.Wiring(4), Is.EqualTo(new[]
        {
            (0, 0), (1, 0), (1, 1), (2, 2), (2, 1), (2, 0), (3, 0), (3, 1), (3, 2), (3, 3),
        }));
    }

    [Test]
    public void Repeat_runs_body_and_records_loop_stats()
    {
        var script = ScriptParser.Parse("SIZE 3\nREPEAT 3\nCYCLE\nSET 0 0 R\nEND");
        var result = new Emulator().Run(script);
        Assert.That(result.InstructionsExecuted, Is.EqualTo(6));
        Assert.That(result.Loops, Is.EqualTo(new[] { new LoopStats(3, null, null) }));
    }

    [Test]
    public void Pattern_instructions_are_not_supported_yet()
    {
        Assert.Throws<NotSupportedException>(() => Run("CHASE", 3));
    }
}
