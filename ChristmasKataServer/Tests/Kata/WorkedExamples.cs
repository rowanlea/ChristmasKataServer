using WebAPI.Kata;

namespace Tests.Kata;

public class WorkedExamples
{
    private static string Answer(string script) =>
        new Emulator().Run(ScriptParser.Parse(script)).Final.ToAnswerLine();

    [TestCase("SIZE 3\nFILL G\nSET 0 0 W\nROW 2 R\nROTATE", "RRGRGW")]
    [TestCase("SIZE 4\nSET 3 0 R\nSET 2 1 G\nSET 0 0 B\nMIRROR\nROTATE", "KKKKGKRKKB")]
    [TestCase("SIZE 4\nSET 0 0 R\nSET 3 0 G\nSET 3 3 B\nREPEAT 2\nCYCLE\nGROW R\nROTATE\nEND", "RRRKKKBKKW")]
    public void Paint_and_transform_examples(string script, string expected) =>
        Assert.That(Answer(script), Is.EqualTo(expected));

    [Category("Task4")]
    [TestCase("SIZE 4\nROW 0 W\nREPEAT 2\nFALL\nSEQUENCE G\nEND\nROTATE\nREPEAT 3\nCHASE\nEND", "KKGKKKKGKK")]
    [TestCase("SIZE 4\nSET 0 0 R\nSET 1 0 G\nSET 1 1 B\nFALL", "KKKKRKKGBK")]
    [TestCase("SIZE 4\n# comments and blank lines are ignored, even inside blocks\nSET 0 0 R\nROW 3 B\n\nREPEAT 0\nFILL W\nEND\nREPEAT 1\nREPEAT 2\n# nothing here\nEND\nREPEAT 3\nCYCLE\nREPEAT 2\nCHASE\nEND\nEND\nEND\nSET 3 1 G", "KKGGGGWGKK")]
    public void Pattern_examples(string script, string expected) =>
        Assert.That(Answer(script), Is.EqualTo(expected));

    [Category("Task5")]
    [TestCase("SIZE 4\nSET 2 1 R\nSET 3 0 G\nREPEAT 1000000000\nCYCLE\nGROW R\nROTATE\nEND", "GGGRRRRRRR")]
    [TestCase("SIZE 4\nSET 0 0 R\nSET 2 0 G\nSET 3 2 B\nSET 1 1 W\nREPEAT 1000000000\nROTATE\nCHASE\nCYCLE\nEND\nREPEAT 999999999\nMIRROR\nCHASE\nEND", "GKKKBKRWKK")]
    public void Long_running_examples(string script, string expected) =>
        Assert.That(Answer(script), Is.EqualTo(expected));
}
