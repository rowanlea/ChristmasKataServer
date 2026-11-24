using System.Text.RegularExpressions;
using WebAPI.Kata;
using WebAPI.Kata.Puzzles;

namespace Tests.Kata.Puzzles;

public class TemplateTests
{
    private static readonly string[] Rounds1To5 = PuzzleCatalogue.All.Where(p => p.Round <= 5).Select(p => p.Id).ToArray();

    private static readonly Dictionary<string, int> LineCaps = new()
    {
        ["1.1"] = 30, ["1.2"] = 40, ["1.3"] = 40, ["1.4"] = 40,
        ["2.1"] = 40, ["2.2"] = 40, ["2.3"] = 70, ["2.4"] = 60,
        ["3.1"] = 12, ["3.2"] = 16, ["3.3"] = 30, ["3.4"] = 16,
        ["4.1"] = 60, ["4.2"] = 140, ["4.3"] = 60, ["4.4"] = 150,
        ["5.1"] = 5000, ["5.2"] = 200, ["5.3"] = 80,
    };

    private static Puzzle P(string id) => PuzzleCatalogue.Find(id)!;

    private static IEnumerable<string> InstructionLines(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')
            && !l.StartsWith("SIZE") && l != "END");

    private static string Script(string id, int seed) => new ChallengeGenerator().Generate(P(id), new Random(seed)).Script;

    [TestCaseSource(nameof(Rounds1To5))]
    public void Every_round_1_to_5_puzzle_generates_for_seeds_1_to_20(string id)
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var challenge = new ChallengeGenerator().Generate(P(id), new Random(seed));

            var script = ScriptParser.Parse(challenge.Script);
            Assert.That(script.Size, Is.EqualTo(16), $"{id} seed {seed}");
            Assert.That(InstructionLines(challenge.Script).Count(), Is.LessThanOrEqualTo(LineCaps[id]), $"{id} seed {seed}");
            var result = new Emulator().Run(script);
            Assert.That(GenerationChecks.Failures(P(id), script, result), Is.Empty, $"{id} seed {seed}");
            Assert.That(challenge.Answer.ToAnswerLine(), Is.EqualTo(result.Final.ToAnswerLine()));
        }
    }

    [TestCaseSource(nameof(Rounds1To5))]
    public void Templates_pass_the_checks_on_at_least_half_of_their_attempts(string id)
    {
        const int attempts = 200;
        var rng = new Random(99);
        var rejected = 0;
        for (var i = 0; i < attempts; i++)
        {
            var script = ScriptParser.Parse(P(id).Template(rng));
            if (GenerationChecks.Failures(P(id), script, new Emulator().Run(script)).Count > 0) rejected++;
        }
        TestContext.Out.WriteLine($"REJECTION {id}: {rejected}/{attempts}");
        Assert.That(rejected, Is.LessThanOrEqualTo(attempts / 2), id);
    }

    [Test]
    public void Round_1_4_has_at_least_three_middle_bulb_sets_on_odd_length_rows()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var middles = ScriptParser.Parse(Script("1.4", seed)).Body
                .OfType<Set>().Count(s => s.Row % 2 == 0 && s.Position == s.Row / 2);
            Assert.That(middles, Is.GreaterThanOrEqualTo(3), $"seed {seed}");
        }
    }

    [Test]
    public void Round_3_3_seeds_a_corner()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var corners = ScriptParser.Parse(Script("3.3", seed)).Body.OfType<Set>()
                .Count(s => (s.Row, s.Position) is (0, 0) or (15, 0) or (15, 15));
            Assert.That(corners, Is.GreaterThanOrEqualTo(1), $"seed {seed}");
        }
    }

    [Test]
    public void Round_3_4_repeat_body_has_both_cycle_and_grow()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var repeat = ScriptParser.Parse(Script("3.4", seed)).Body.OfType<Repeat>().Single();
            Assert.That(repeat.Body.OfType<Cycle>(), Is.Not.Empty, $"seed {seed}");
            Assert.That(repeat.Body.OfType<Grow>(), Is.Not.Empty, $"seed {seed}");
        }
    }

    private static bool Moves(Instruction i) => i is Chase or Sequence || (i is Repeat r && r.Body.Any(Moves));

    [Test]
    public void Round_4_4_has_a_chase_or_sequence_after_a_rotate()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var body = ScriptParser.Parse(Script("4.4", seed)).Body;
            var firstRotate = body.ToList().FindIndex(i => i is Rotate);
            Assert.That(firstRotate, Is.GreaterThanOrEqualTo(0), $"seed {seed}");
            Assert.That(body.Skip(firstRotate + 1).Any(Moves), $"seed {seed}");
        }
    }

    [Test]
    public void Round_5_1_has_5000_instruction_lines_and_no_loops()
    {
        var text = Script("5.1", 1);

        Assert.That(InstructionLines(text).Count(), Is.EqualTo(5000));
        Assert.That(text, Does.Not.Contain("REPEAT"));
        var kinds = ScriptParser.Parse(text).Body.Select(i => i.GetType()).Distinct().Count();
        Assert.That(kinds, Is.EqualTo(10));
    }

    [Test]
    public void Round_5_2_nests_four_deep()
    {
        static int Depth(IReadOnlyList<Instruction> body) =>
            body.OfType<Repeat>().Select(r => 1 + Depth(r.Body)).DefaultIfEmpty(0).Max();

        for (var seed = 1; seed <= 20; seed++)
            Assert.That(Depth(ScriptParser.Parse(Script("5.2", seed)).Body), Is.GreaterThanOrEqualTo(4), $"seed {seed}");
    }

    [Test]
    public void Round_5_3_has_repeat_zero_repeat_one_and_a_comment_inside_a_block()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var text = Script("5.3", seed);
            var body = ScriptParser.Parse(text).Body;
            Assert.That(body.OfType<Repeat>().Any(r => r.Count == 0 && r.Body.Count > 0), $"seed {seed}");
            Assert.That(body.OfType<Repeat>().Any(r => r.Count == 1), $"seed {seed}");
            Assert.That(Regex.IsMatch(text, @"REPEAT[^\n]*\n(?:[^\n]*\n)*?#[^\n]*\n(?:[^\n]*\n)*?END"), $"seed {seed}");
        }
    }

    [Test]
    public void Rounds_6_and_7_stay_placeholders_until_task_8()
    {
        Assert.Throws<NotImplementedException>(() => P("6.1").Template(new Random(1)));
        Assert.Throws<NotImplementedException>(() => P("7.1").Template(new Random(1)));
    }
}
