using WebAPI.Kata;

namespace Tests.Kata;

public class ScriptParserTests
{
    [Test]
    public void Accepts_indented_block_bodies()
    {
        var indented = ScriptParser.Parse("SIZE 4\nREPEAT 2\n  CYCLE\n\tREPEAT 3 \n    CHASE\n  END\nEND\n");
        var flat = ScriptParser.Parse("SIZE 4\nREPEAT 2\nCYCLE\nREPEAT 3\nCHASE\nEND\nEND");

        var outer = (Repeat)indented.Body[0];
        var flatOuter = (Repeat)flat.Body[0];
        Assert.That(outer.Count, Is.EqualTo(flatOuter.Count));
        Assert.That(outer.Body[0], Is.EqualTo(new Cycle()));
        var inner = (Repeat)outer.Body[1];
        Assert.That(inner.Count, Is.EqualTo(3));
        Assert.That(inner.Body, Is.EqualTo(new Instruction[] { new Chase() }));
    }

    [Test]
    public void Indented_comment_is_ignored()
    {
        var script = ScriptParser.Parse("SIZE 4\nREPEAT 1\n    # nothing here\nEND");

        Assert.That(((Repeat)script.Body[0]).Body, Is.Empty);
    }

    [Test]
    public void Parses_nested_repeat()
    {
        var script = ScriptParser.Parse("SIZE 4\nREPEAT 3\nCYCLE\nREPEAT 2\nCHASE\nEND\nEND");

        Assert.That(script.Size, Is.EqualTo(4));
        Assert.That(script.Body, Has.Count.EqualTo(1));
        var outer = (Repeat)script.Body[0];
        Assert.That(outer.Count, Is.EqualTo(3));
        Assert.That(outer.Body, Has.Count.EqualTo(2));
        Assert.That(outer.Body[0], Is.EqualTo(new Cycle()));
        var inner = (Repeat)outer.Body[1];
        Assert.That(inner.Count, Is.EqualTo(2));
        Assert.That(inner.Body, Is.EqualTo(new Instruction[] { new Chase() }));
    }

    [Test]
    public void Parses_every_instruction()
    {
        var script = ScriptParser.Parse(
            "SIZE 5\nSET 3 2 R\nROW 4 G\nFILL B\nROTATE\nMIRROR\nGROW W\nCYCLE\nSEQUENCE K\nCHASE\nFALL");

        Assert.That(script.Body, Is.EqualTo(new Instruction[]
        {
            new Set(3, 2, Bulb.R), new Row(4, Bulb.G), new Fill(Bulb.B), new Rotate(), new Mirror(),
            new Grow(Bulb.W), new Cycle(), new Sequence(Bulb.K), new Chase(), new Fall(),
        }));
    }

    [Test]
    public void Ignores_blank_and_comment_lines()
    {
        var script = ScriptParser.Parse("# header\n\nSIZE 4\n\n# note\nREPEAT 2\n\n# inside\nCHASE\n\nEND\n\n");

        var repeat = (Repeat)script.Body.Single();
        Assert.That(repeat.Body, Is.EqualTo(new Instruction[] { new Chase() }));
    }

    [Test]
    public void Accepts_CRLF_line_endings()
    {
        var script = ScriptParser.Parse("SIZE 4\r\nSET 1 1 W\r\nREPEAT 2\r\nFALL\r\nEND\r\n");

        Assert.That(script.Body, Has.Count.EqualTo(2));
        Assert.That(script.Body[0], Is.EqualTo(new Set(1, 1, Bulb.W)));
    }

    [Test]
    public void Accepts_repeat_bounds()
    {
        Assert.That(((Repeat)ScriptParser.Parse("SIZE 4\nREPEAT 0\nEND").Body[0]).Count, Is.EqualTo(0));
        Assert.That(((Repeat)ScriptParser.Parse("SIZE 4\nREPEAT 1000000000\nEND").Body[0]).Count,
            Is.EqualTo(1_000_000_000));
    }

    [TestCase("", 1, TestName = "Empty_script")]
    [TestCase("CYCLE\nSIZE 4", 1, TestName = "SIZE_not_first")]
    [TestCase("# c\n\nCYCLE", 3, TestName = "No_SIZE_after_comments")]
    [TestCase("SIZE 4\nSIZE 4", 2, TestName = "Repeated_SIZE")]
    [TestCase("SIZE 4\nCYCLE\nTWINKLE", 3, TestName = "Unknown_instruction")]
    [TestCase("SIZE 4\nFILL", 2, TestName = "Too_few_tokens")]
    [TestCase("SIZE 4\nCYCLE\nCHASE R", 3, TestName = "Too_many_tokens")]
    [TestCase("SIZE 4\nSET  1 1 R", 2, TestName = "Double_space")]
    [TestCase("SIZE 4\nfill R", 2, TestName = "Lowercase_instruction")]
    [TestCase("SIZE 4\nFILL r", 2, TestName = "Lowercase_state")]
    [TestCase("SIZE 4\nFILL X", 2, TestName = "State_outside_RGBWK")]
    [TestCase("SIZE 4\nFILL RG", 2, TestName = "State_too_long")]
    [TestCase("SIZE 4\nSET 3 4 R", 2, TestName = "Position_beyond_row")]
    [TestCase("SIZE 4\nSET 4 0 R", 2, TestName = "Row_beyond_tree_in_SET")]
    [TestCase("SIZE 4\nROW 4 R", 2, TestName = "ROW_beyond_tree")]
    [TestCase("SIZE 4\nREPEAT -1\nEND", 2, TestName = "Negative_repeat")]
    [TestCase("SIZE 4\nREPEAT 1000000001\nEND", 2, TestName = "Huge_repeat")]
    [TestCase("SIZE 4\nREPEAT 99999999999999999999\nEND", 2, TestName = "Overflowing_repeat")]
    [TestCase("SIZE 4\nREPEAT x\nEND", 2, TestName = "Non_numeric_repeat")]
    [TestCase("SIZE 4\nCYCLE\nEND", 3, TestName = "Unmatched_END")]
    [TestCase("SIZE 4\nREPEAT 2\nCYCLE", 2, TestName = "Unclosed_REPEAT")]
    [TestCase("SIZE 4\nREPEAT 2\nREPEAT 3\nEND", 2, TestName = "Unclosed_outer_REPEAT")]
    [TestCase("SIZE 0", 1, TestName = "SIZE_zero")]
    [TestCase("SIZE 49", 1, TestName = "SIZE_too_big")]
    public void Rejects_with_line_number(string text, int expectedLine)
    {
        var error = Assert.Throws<ScriptFormatException>(() => ScriptParser.Parse(text));

        Assert.That(error!.LineNumber, Is.EqualTo(expectedLine));
    }
}
