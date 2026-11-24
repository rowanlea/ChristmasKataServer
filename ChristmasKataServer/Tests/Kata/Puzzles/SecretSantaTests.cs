using System.Diagnostics;
using WebAPI.Kata;
using WebAPI.Kata.Puzzles;
using WebAPI.Kata.Puzzles.Templates;

namespace Tests.Kata.Puzzles;

public class SecretSantaTests
{
    [Test]
    public void Secret_santa_script_ends_on_picture()
    {
        for (var seed = 1; seed <= 5; seed++)
        {
            var script = SecretSanta.Template(new Random(seed));
            var expected = SecretSanta.Picture(new Random(seed));

            var result = new Emulator().Run(ScriptParser.Parse(script));

            Assert.That(result.Final.ToAnswerLine(), Is.EqualTo(expected.ToAnswerLine()), $"seed {seed}");
        }
    }

    [Test]
    public void Secret_santa_runs_in_time()
    {
        var script = ScriptParser.Parse(SecretSanta.Template(new Random(7)));
        var clock = Stopwatch.StartNew();
        new Emulator().Run(script);
        Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(3));
    }

    [Test]
    public void Picture_varies()
    {
        var pictures = Enumerable.Range(1, 20).Select(s => SecretSanta.Picture(new Random(s)).ToAnswerLine()).ToList();
        Assert.That(pictures.Distinct().Count(), Is.EqualTo(pictures.Count));

        var facings = new HashSet<bool>();
        var offsets = new HashSet<int>();
        var sacks = new HashSet<char>();
        for (var s = 1; s <= 20; s++)
        {
            var layout = SecretSanta.Layout(new Random(s));
            facings.Add(layout.FacingLeft);
            offsets.Add(layout.Offset);
            sacks.Add(layout.Sack);
        }
        Assert.That(facings.Count, Is.GreaterThan(1));
        Assert.That(offsets.Count, Is.GreaterThan(1));
        Assert.That(sacks.Count, Is.GreaterThan(1));
    }

    [Test]
    public void Art_fits_scene()
    {
        var lines = SecretSanta.ArtLines();
        Assert.That(lines, Has.Count.EqualTo(14));
        foreach (var line in lines)
        {
            Assert.That(line.Length, Is.LessThanOrEqualTo(35));
            Assert.That(line.All(c => "RGBWK.S".Contains(c)), Is.True, line);
        }
    }

    [Test]
    public void Orbit_period_of_a_known_body()
    {
        var tree = new Tree(4);
        tree[3, 1] = Bulb.R;
        Assert.That(SecretSanta.OrbitPeriod(tree, [new Cycle()], 100), Is.EqualTo(4));
        Assert.That(SecretSanta.OrbitPeriod(tree, [new Chase()], 5), Is.EqualTo(-1));
        Assert.That(SecretSanta.OrbitPeriod(tree, [new Chase()], 10), Is.EqualTo(10));
    }

    [Test]
    public void Secret_santa_generates_through_the_generator_with_both_transforms()
    {
        var puzzle = PuzzleCatalogue.Find("7.1")!;
        for (var seed = 1; seed <= 10; seed++)
        {
            var clock = Stopwatch.StartNew();
            var challenge = new ChallengeGenerator().Generate(puzzle, new Random(seed));
            var script = ScriptParser.Parse(challenge.Script);
            var result = new Emulator().Run(script);
            clock.Stop();

            var loops = script.Body.OfType<Repeat>().SelectMany(r => r.Body).ToList();
            Assert.That(loops.OfType<Rotate>(), Is.Not.Empty, $"seed {seed}");
            Assert.That(loops.OfType<Mirror>(), Is.Not.Empty, $"seed {seed}");
            Assert.That(result.Final.ToAnswerLine(), Is.EqualTo(challenge.Answer.ToAnswerLine()), $"seed {seed}");
            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(5), $"seed {seed}");
            TestContext.Out.WriteLine($"seed {seed}: {clock.ElapsedMilliseconds} ms");
        }
    }
}
