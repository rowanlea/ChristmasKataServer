using WebAPI.Kata;
using WebAPI.Kata.Puzzles;

namespace Tests.Kata.Puzzles;

public class GenerationChecksTests
{
    private static IReadOnlyList<string> Check(string id, string script)
    {
        var parsed = ScriptParser.Parse(script);
        var result = new Emulator().Run(parsed);
        return GenerationChecks.Failures(PuzzleCatalogue.Find(id)!, parsed, result);
    }

    private static readonly string Pattern = new ScriptBuilder().Size(16).RandomSet(new Random(7), 120).ToString();

    [Test]
    public void Symmetric_rotation_fails_wrong_rotation()
    {
        Assert.That(Check("1.3", "SIZE 16\nFILL R\nROTATE\n"), Has.Some.Contains("wrong rotation"));
    }

    [Test]
    public void Rotation_inside_a_loop_counts()
    {
        Assert.That(Check("1.3", "SIZE 16\nFILL R\nREPEAT 2\nROTATE\nEND\n"), Has.Some.Contains("wrong rotation"));
    }

    [Test]
    public void Asymmetric_rotation_and_mirror_pass()
    {
        Assert.That(Check("1.3", Pattern + "ROTATE\n"), Is.Empty);
        Assert.That(Check("1.4", Pattern + "MIRROR\n"), Is.Empty);
    }

    [Test]
    public void Symmetric_mirror_fails_wrong_axis()
    {
        Assert.That(Check("1.4", "SIZE 16\nFILL R\nMIRROR\n"), Has.Some.Contains("wrong mirror"));
    }

    [Test]
    public void Empty_final_tree_fails()
    {
        Assert.That(Check("1.1", "SIZE 16\nFILL R\nFILL K\n"), Has.Some.Contains("drains to empty"));
    }

    [Test]
    public void Round_4_script_that_ignores_the_wiring_fails()
    {
        Assert.That(Check("4.1", "SIZE 16\nFILL R\nSEQUENCE G\n"), Has.Some.Contains("answer-order wiring"));
    }

    [Test]
    public void Round_6_power_of_two_period_fails()
    {
        var failures = Check("6.1", "SIZE 16\nFILL R\nREPEAT 1000000000\nCYCLE\nEND\n");

        Assert.That(failures, Has.Some.Contains("power-of-two period"));
    }

    [Test]
    public void Round_6_period_of_three_passes_the_period_check()
    {
        var failures = Check("6.1", Pattern + "REPEAT 1000000000\nROTATE\nEND\n");

        Assert.That(failures, Is.Empty);
    }

    [Test]
    public void Puzzle_6_2_needs_a_long_pre_period()
    {
        var failures = Check("6.2", Pattern + "REPEAT 1000000000\nROTATE\nEND\n");

        Assert.That(failures, Has.Some.Contains("pre-period"));
    }

    [Test]
    public void Puzzle_6_2_with_slow_growth_passes()
    {
        var failures = Check("6.2", "SIZE 48\nSET 0 0 R\nREPEAT 1000000000\nGROW R\nEND\n");

        Assert.That(failures, Has.None.Contains("pre-period"));
    }

    [Test]
    public void Round_5_brute_force_over_budget_fails_without_hanging()
    {
        var failures = Check("5.2", "SIZE 16\nSET 15 0 R\nREPEAT 1000000000\nREPEAT 1000000000\nEND\nEND\n");

        Assert.That(failures, Has.Some.Contains("instructions"));
    }

    [Test]
    public void Round_5_small_script_passes_the_budget()
    {
        var failures = Check("5.2", "SIZE 16\nSET 15 0 R\nREPEAT 100\nSET 4 1 G\nEND\n");

        Assert.That(failures, Has.None.Contains("instructions"));
    }

    [Test]
    public void Round_2_swap_that_changes_nothing_fails()
    {
        Assert.That(Check("2.1", "SIZE 16\nFILL R\nROTATE\nMIRROR\n"), Has.Some.Contains("swap"));
    }

    [Test]
    public void Round_2_swap_that_changes_the_answer_passes()
    {
        Assert.That(Check("2.1", Pattern + "ROTATE\nMIRROR\n"), Is.Empty);
    }

    [Test]
    public void Round_2_passes_when_one_swap_matters_and_another_does_not()
    {
        var sets = Pattern["SIZE 16\n".Length..];
        var failures = Check("2.1", "SIZE 16\nFILL R\nROTATE\nMIRROR\n" + sets + "ROTATE\nMIRROR\n");

        Assert.That(failures, Has.None.Contains("swap"));
    }

    [Test]
    public void Round_2_swap_inside_a_loop_body_is_checked()
    {
        var failures = Check("2.3", "SIZE 16\nFILL R\nREPEAT 3\nROTATE\nMIRROR\nEND\n");

        Assert.That(failures, Has.Some.Contains("swap"));
    }

    [Test]
    public void Round_2_without_adjacent_transforms_passes_the_swap_check()
    {
        Assert.That(Check("2.4", Pattern + "ROTATE\nROW 15 G\nMIRROR\n"), Is.Empty);
    }

    [Test]
    public void Puzzle_7_1_needs_a_mostly_lit_answer()
    {
        var sparse = Check("7.1", "SIZE 48\nSET 47 0 R\n");
        var lit = Check("7.1", "SIZE 48\nFILL R\nSET 47 0 G\nSET 47 20 B\nSET 30 1 W\n");

        Assert.That(sparse, Has.Some.Contains("all-K"));
        Assert.That(lit, Is.Empty);
    }

    [Test]
    public void Similarity_is_the_fraction_of_equal_bulbs()
    {
        var a = new Tree(2);
        var b = new Tree(2);
        b[1, 0] = Bulb.R;

        Assert.That(GenerationChecks.Similarity(a, b), Is.EqualTo(2.0 / 3.0).Within(1e-9));
    }

    [Test]
    public void Instruction_budget_stops_empty_loops()
    {
        var script = ScriptParser.Parse("SIZE 16\nREPEAT 1000000000\nEND\n");
        var emulator = new Emulator(new EmulatorOptions(DetectCycles: false, InstructionBudget: 1000));

        Assert.Throws<InstructionBudgetExceededException>(() => emulator.Run(script));
    }
}
