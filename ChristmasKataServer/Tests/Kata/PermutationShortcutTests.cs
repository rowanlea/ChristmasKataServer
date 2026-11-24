using System.Text;
using WebAPI.Kata;

namespace Tests.Kata;

public class PermutationShortcutTests
{
    private static readonly string[] Ops = ["ROTATE", "MIRROR", "CHASE", "CYCLE"];
    private static readonly long[] BigCounts = [0, 1, 2, 3, 7, 12, 31, 100, 999_999_999, 1_000_000_000];

    private static string Body(Random rng, StringBuilder text, int depth, long[] counts)
    {
        for (var i = rng.Next(1, 5); i > 0; i--)
        {
            if (depth > 0 && rng.Next(4) == 0)
            {
                text.Append($"REPEAT {counts[rng.Next(counts.Length)]}\n");
                Body(rng, text, depth - 1, counts);
                text.Append("END\n");
            }
            else text.Append(Ops[rng.Next(Ops.Length)]).Append('\n');
        }
        return text.ToString();
    }

    private static string Script(Random rng, long[] counts)
    {
        var size = rng.Next(1, 7);
        var text = new StringBuilder($"SIZE {size}\n");
        for (var i = rng.Next(1, 2 * size * size); i > 0; i--)
        {
            var r = rng.Next(size);
            text.Append($"SET {r} {rng.Next(r + 1)} {"RGBWK"[rng.Next(5)]}\n");
        }
        text.Append($"REPEAT {counts[rng.Next(counts.Length)]}\n");
        Body(rng, text, 2, counts);
        text.Append("END\n");
        return text.ToString();
    }

    private static Quirks QuirksFor(int i) => (Quirks)(i % 8);

    [Test]
    public void Shortcut_matches_plain_detection_including_loop_stats()
    {
        var rng = new Random(2024);
        for (var i = 0; i < 600; i++)
        {
            var script = ScriptParser.Parse(Script(rng, BigCounts));
            var fast = new Emulator(new EmulatorOptions(QuirksFor(i))).Run(script);
            var plain = new Emulator(new EmulatorOptions(QuirksFor(i), PermutationShortcut: false)).Run(script);

            Assert.That(fast.Final.ToAnswerLine(), Is.EqualTo(plain.Final.ToAnswerLine()), $"case {i}");
            Assert.That(fast.Loops, Is.EqualTo(plain.Loops), $"case {i}");
        }
    }

    [Test]
    public void Shortcut_matches_pass_by_pass_run_for_small_counts()
    {
        var rng = new Random(77);
        long[] small = [0, 1, 2, 3, 5, 8, 13, 40];
        for (var i = 0; i < 600; i++)
        {
            var script = ScriptParser.Parse(Script(rng, small));
            var fast = new Emulator(new EmulatorOptions(QuirksFor(i))).Run(script);
            var brute = new Emulator(new EmulatorOptions(QuirksFor(i), DetectCycles: false)).Run(script);

            Assert.That(fast.Final.ToAnswerLine(), Is.EqualTo(brute.Final.ToAnswerLine()), $"case {i}");
        }
    }
}
