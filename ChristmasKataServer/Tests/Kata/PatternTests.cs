using WebAPI.Kata;

namespace Tests.Kata;

public class PatternTests
{
    private static Tree Run(string body, int size, EmulatorOptions? options = null) =>
        new Emulator(options).Run(ScriptParser.Parse($"SIZE {size}\n{body}")).Final;

    [Test]
    public void Wiring_on_four_rows()
    {
        Assert.That(Geometry.Wiring(4), Is.EqualTo(new[]
        {
            (0, 0), (1, 0), (1, 1), (2, 2), (2, 1), (2, 0), (3, 0), (3, 1), (3, 2), (3, 3),
        }));
    }

    [Test]
    public void Sequence_follows_wiring()
    {
        var tree = Run("SEQUENCE G\nSEQUENCE G\nSEQUENCE G\nSEQUENCE G", 4);
        Assert.That(tree.ToAnswerLine(), Is.EqualTo("GGGKKGKKKK"));
    }

    [Test]
    public void Sequence_on_full_tree_does_nothing()
    {
        var tree = Run("FILL R\nSEQUENCE G", 4);
        Assert.That(tree.ToAnswerLine(), Is.EqualTo("RRRRRRRRRR"));
    }

    [Test]
    public void Chase_wraps_last_to_star()
    {
        Assert.That(Run("SET 3 3 R\nCHASE", 4).ToAnswerLine(), Is.EqualTo("RKKKKKKKKK"));
    }

    [Test]
    public void Chase_reverses_on_even_rows()
    {
        Assert.That(Run("SET 1 1 B\nCHASE", 4).ToAnswerLine(), Is.EqualTo("KKKKKBKKKK"));
    }

    [Test]
    public void Fall_drops_two_rows()
    {
        Assert.That(Run("SET 0 0 R\nSET 1 0 G\nSET 1 1 B\nFALL", 4).ToAnswerLine(), Is.EqualTo("KKKKRKKGBK"));
    }

    [Test]
    public void Fall_loses_bottom_states_and_clears_top_rows()
    {
        Assert.That(Run("FILL R\nFALL", 4).ToAnswerLine(), Is.EqualTo("KKKKRKKRRK"));
        Assert.That(Run("ROW 1 G\nROW 2 R\nROW 3 B\nFALL", 4).ToAnswerLine(), Is.EqualTo("KKKKKKKGGK"));
    }

    [Test]
    public void Answer_order_quirk_changes_chase()
    {
        var tree = Run("SET 1 1 B\nCHASE", 4, new EmulatorOptions(Quirks.AnswerOrderWiring));
        Assert.That(tree.ToAnswerLine(), Is.EqualTo("KKKBKKKKKK"));
    }
}
