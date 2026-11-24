namespace WebAPI.Kata.Puzzles;

public interface IChallengeGenerator
{
    GeneratedChallenge Generate(Puzzle puzzle, Random rng);
}

public sealed class ChallengeGenerator(long instructionBudget = ChallengeGenerator.DefaultInstructionBudget)
    : IChallengeGenerator
{
    public const long DefaultInstructionBudget = 2_000_000;
    private const int MaxAttempts = 50;

    public GeneratedChallenge Generate(Puzzle puzzle, Random rng)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var text = puzzle.Template(rng);
            var script = ScriptParser.Parse(text);
            RunResult result;
            try
            {
                result = new Emulator(new EmulatorOptions(InstructionBudget: instructionBudget)).Run(script);
            }
            catch (InstructionBudgetExceededException)
            {
                continue;
            }
            if (GenerationChecks.Failures(puzzle, script, result).Count == 0)
                return new GeneratedChallenge(text, result.Final);
        }
        throw new InvalidOperationException($"No acceptable script for puzzle {puzzle.Id} after {MaxAttempts} attempts");
    }
}
