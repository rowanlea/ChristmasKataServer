using System.Diagnostics;
using WebAPI.Kata;

namespace Tests.Kata;

public class RepeatTests
{
    private const string Seeds = "SET 2 1 R\nSET 9 4 G\nSET 5 3 B\n";

    private static RunResult Run(string body, int size = 16, bool detectCycles = true) =>
        new Emulator(new EmulatorOptions(DetectCycles: detectCycles))
            .Run(ScriptParser.Parse($"SIZE {size}\n{body}"));

    private static string Plain(string body, int size = 16) => Run(body, size).Final.ToAnswerLine();

    private static string Passes(string setup, string pass, long count) =>
        Plain(setup + string.Concat(Enumerable.Repeat(pass, (int)count)));

    [Test]
    public void Repeat_zero_skips_body()
    {
        var result = Run("SET 0 0 R\nREPEAT 0\n  FILL W\nEND");
        Assert.That(result.Final.ToAnswerLine(), Is.EqualTo(Plain("SET 0 0 R")));
        Assert.That(result.InstructionsExecuted, Is.EqualTo(1));
    }

    [Test]
    public void Repeat_inside_zero_pass_loop_has_no_loop_stats()
    {
        var result = Run("REPEAT 0\n  REPEAT 5\n    CHASE\n  END\nEND");
        Assert.That(result.Loops, Has.Count.EqualTo(1));
        Assert.That(result.Loops[0].Count, Is.EqualTo(0));
    }

    [Test]
    public void Nested_repeat_counts_multiply()
    {
        var nested = Plain("SET 0 0 R\nSET 5 2 G\nREPEAT 3\n  REPEAT 4\n    CHASE\n  END\nEND");
        var flat = Plain("SET 0 0 R\nSET 5 2 G\n" + string.Concat(Enumerable.Repeat("CHASE\n", 12)));
        Assert.That(nested, Is.EqualTo(flat));
    }

    [Test, CancelAfter(10000)]
    public void Billion_rotate_cycle_finishes_fast()
    {
        var watch = Stopwatch.StartNew();
        var result = Run(Seeds + "REPEAT 1000000000\n  ROTATE\n  CYCLE\nEND");
        watch.Stop();

        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
        Assert.That(result.Loops[0].Period, Is.EqualTo(12));
        Assert.That(result.Final.ToAnswerLine(), Is.EqualTo(Passes(Seeds, "ROTATE\nCYCLE\n", 1000000000 % 12)));
    }

    [Test, CancelAfter(10000)]
    public void False_start_pre_period()
    {
        var watch = Stopwatch.StartNew();
        var result = Run("SET 2 1 R\nSET 9 4 G\nREPEAT 1000000000\n  CYCLE\n  GROW R\n  ROTATE\nEND");
        watch.Stop();

        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
        Assert.That(result.Loops[0].PrePeriod, Is.EqualTo(43));
        Assert.That(result.Loops[0].Period, Is.EqualTo(12));
        Assert.That(result.Final.ToAnswerLine(), Is.EqualTo(
            Passes("SET 2 1 R\nSET 9 4 G\n", "CYCLE\nGROW R\nROTATE\n", 43 + (1000000000 - 43) % 12)));
    }

    [Test, CancelAfter(10000)]
    public void Deep_sleep_inner_period()
    {
        const string body = "  ROTATE\n  CHASE\n  CYCLE\n";
        Assert.That(Run(Seeds + $"REPEAT 1000000000\n{body}END").Loops[0].Period, Is.EqualTo(372));

        var watch = Stopwatch.StartNew();
        var result = Run(Seeds + $"REPEAT 1000\n  SET 0 0 W\n  REPEAT 1000000000\n  {body}  END\nEND");
        watch.Stop();

        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        Assert.That(result.Loops, Has.Count.EqualTo(2));
    }

    [Test, CancelAfter(10000)]
    public void Detection_off_matches_on()
    {
        const string script = Seeds + "REPEAT 5000\n  ROTATE\n  CHASE\n  CYCLE\nEND";
        var off = Run(script, detectCycles: false);
        var on = Run(script);

        Assert.That(off.Final.ToAnswerLine(), Is.EqualTo(on.Final.ToAnswerLine()));
        Assert.That(off.InstructionsExecuted, Is.EqualTo(5000 * 3 + 3));
        Assert.That(off.Loops[0].Period, Is.Null);
    }
}
