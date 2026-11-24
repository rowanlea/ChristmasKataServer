using System.Diagnostics;
using WebAPI.Kata;
using WebAPI.Kata.Puzzles;

namespace Tests.Kata.Puzzles;

public class SilentNightTests
{
    private static readonly string[] Ids = ["6.1", "6.2", "6.3"];

    [TestCaseSource(nameof(Ids))]
    public void Round_6_generates_for_seeds_1_to_10(string id)
    {
        var puzzle = PuzzleCatalogue.Find(id)!;
        for (var seed = 1; seed <= 10; seed++)
        {
            var clock = Stopwatch.StartNew();
            var challenge = new ChallengeGenerator().Generate(puzzle, new Random(seed));
            var script = ScriptParser.Parse(challenge.Script);
            var result = new Emulator().Run(script);
            clock.Stop();

            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(5), $"{id} seed {seed}");
            Assert.That(GenerationChecks.Failures(puzzle, script, result), Is.Empty, $"{id} seed {seed}");
            Assert.That(result.Final.ToAnswerLine(), Is.EqualTo(challenge.Answer.ToAnswerLine()));
            foreach (var loop in result.Loops)
                Assert.That(loop.Count == 25 && id == "6.3" || loop.Count >= 999_000_000,
                    Is.True, $"{id} seed {seed}: loop count {loop.Count}");
            if (id == "6.2")
                Assert.That(result.Loops.Single().PrePeriod, Is.GreaterThanOrEqualTo(20), $"seed {seed}");
        }
    }

    [Test]
    public void Round_6_loops_are_fast_forwarded_by_period()
    {
        var challenge = new ChallengeGenerator().Generate(PuzzleCatalogue.Find("6.1")!, new Random(3));
        var result = new Emulator().Run(ScriptParser.Parse(challenge.Script));
        Assert.That(result.Loops.Single().Period, Is.GreaterThan(1));
    }
}
