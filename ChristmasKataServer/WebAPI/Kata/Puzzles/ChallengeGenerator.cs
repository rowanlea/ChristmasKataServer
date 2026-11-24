namespace WebAPI.Kata.Puzzles;

public interface IChallengeGenerator
{
    GeneratedChallenge Generate(Puzzle puzzle, Random rng);
}

public sealed class ChallengeGenerator : IChallengeGenerator
{
    private const int MaxAttempts = 50;

    public GeneratedChallenge Generate(Puzzle puzzle, Random rng)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var text = puzzle.Template(rng);
            var script = ScriptParser.Parse(text);
            var result = new Emulator().Run(script);
            if (GenerationChecks.Failures(puzzle, script, result).Count == 0)
                return new GeneratedChallenge(text, result.Final);
        }
        throw new InvalidOperationException($"No acceptable script for puzzle {puzzle.Id} after {MaxAttempts} attempts");
    }
}
