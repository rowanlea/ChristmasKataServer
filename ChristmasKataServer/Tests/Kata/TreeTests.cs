using WebAPI.Kata;

namespace Tests.Kata;

public class TreeTests
{
    [Test]
    public void Index_matches_answer_order()
    {
        Assert.That(Tree.Index(0, 0), Is.EqualTo(0));
        Assert.That(Tree.Index(1, 1), Is.EqualTo(2));
        Assert.That(Tree.Index(2, 0), Is.EqualTo(3));
        Assert.That(Tree.Index(15, 15), Is.EqualTo(135));
    }

    [Test]
    public void New_tree_is_all_off()
    {
        Assert.That(new Tree(16).ToAnswerLine(), Is.EqualTo(new string('K', 136)));
    }

    [Test]
    public void Answer_line_round_trips()
    {
        var tree = Tree.FromAnswerLine("RRGRGW", 3);

        Assert.That(tree.ToAnswerLine(), Is.EqualTo("RRGRGW"));
        Assert.That(tree[2, 1], Is.EqualTo(Bulb.G));
    }

    [Test]
    public void Clone_is_independent()
    {
        var tree = new Tree(3);
        var copy = tree.Clone();
        copy[1, 0] = Bulb.B;

        Assert.That(tree[1, 0], Is.EqualTo(Bulb.K));
        Assert.That(copy.Count, Is.EqualTo(6));
    }

    [Test]
    public void Contains_rejects_positions_outside_the_triangle()
    {
        var tree = new Tree(4);

        Assert.That(tree.Contains(3, 3), Is.True);
        Assert.That(tree.Contains(2, 3), Is.False);
        Assert.That(tree.Contains(4, 0), Is.False);
        Assert.That(tree.Contains(0, -1), Is.False);
    }

    [Test]
    public void FromAnswerLine_rejects_wrong_length_and_bad_chars()
    {
        Assert.Throws<FormatException>(() => Tree.FromAnswerLine("RRG", 3));
        Assert.Throws<FormatException>(() => Tree.FromAnswerLine("RRGRGX", 3));
    }
}
