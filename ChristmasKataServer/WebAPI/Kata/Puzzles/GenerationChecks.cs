namespace WebAPI.Kata.Puzzles;

public static class GenerationChecks
{
    private const double MaxSimilarity = 0.60;
    private const double MaxAllKSimilarity = 0.10;
    private const long MaxBruteForceInstructions = 200_000;
    private const int MinFalseStartPrePeriod = 20;

    public static IReadOnlyList<string> Failures(Puzzle puzzle, Script script, RunResult result)
    {
        var failures = new List<string>();
        var answer = result.Final;

        if (IsAllOff(answer)) failures.Add("final tree drains to empty (all-K)");

        if (Contains<Rotate>(script.Body)
            && WrongRunSimilarity(puzzle, answer, Rerun(script, Quirks.CounterClockwiseRotate)) >= MaxSimilarity)
            failures.Add("wrong rotation scores 60% or more");
        if (Contains<Mirror>(script.Body)
            && WrongRunSimilarity(puzzle, answer, Rerun(script, Quirks.MirrorWrongAxis)) >= MaxSimilarity)
            failures.Add("wrong mirror axis scores 60% or more");

        switch (puzzle.Round)
        {
            case 2 when SwapLeavesAnswerUnchanged(script, answer):
                failures.Add("no adjacent transform pair is order-sensitive (swap)");
                break;
            case 4 when WiringIsIndistinguishable(script, answer):
                failures.Add("answer-order wiring scores too close to the answer");
                break;
            case 5 when ExceedsInstructionBudget(script):
                failures.Add($"brute force runs over {MaxBruteForceInstructions} instructions");
                break;
            case 6:
                failures.AddRange(RoundSixFailures(puzzle, result));
                break;
        }

        if (puzzle.Id == "7.1" && Similarity(answer, new Tree(answer.Size)) >= MaxAllKSimilarity)
            failures.Add("answer too close to all-K");

        return failures;
    }

    public static double Similarity(Tree a, Tree b)
    {
        var x = a.ToAnswerLine();
        var y = b.ToAnswerLine();
        var equal = 0;
        for (var i = 0; i < x.Length; i++)
            if (x[i] == y[i]) equal++;
        return (double)equal / x.Length;
    }

    /// <summary>Secret Santa is mostly background green, so its wrong runs are compared over the scene bulbs only.</summary>
    private static double WrongRunSimilarity(Puzzle puzzle, Tree answer, Tree wrong)
    {
        if (puzzle.Id != "7.1") return Similarity(answer, wrong);
        var x = answer.ToAnswerLine();
        var y = wrong.ToAnswerLine();
        var scene = 0;
        var equal = 0;
        for (var i = 0; i < x.Length; i++)
        {
            if (x[i] == 'G') continue;
            scene++;
            if (x[i] == y[i]) equal++;
        }
        return scene == 0 ? 1.0 : (double)equal / scene;
    }

    private static IEnumerable<string> RoundSixFailures(Puzzle puzzle, RunResult result)
    {
        foreach (var loop in result.Loops.Where(l => l.Period is not null))
        {
            var period = loop.Period!.Value;
            if ((period & (period - 1)) == 0) yield return $"loop has power-of-two period {period}";
        }
        if (puzzle.Id == "6.2" && result.Loops.Any(l => l.PrePeriod is < MinFalseStartPrePeriod))
            yield return $"pre-period shorter than {MinFalseStartPrePeriod}";
    }

    private static bool WiringIsIndistinguishable(Script script, Tree answer)
    {
        if (Contains<Chase>(script.Body))
            return Similarity(answer, Rerun(script, Quirks.AnswerOrderWiring)) >= MaxSimilarity;
        if (Contains<Sequence>(script.Body))
            return Similarity(answer, Rerun(script, Quirks.AnswerOrderWiring)) >= 1.0;
        return false;
    }

    private static bool IsAllOff(Tree tree) => tree.ToAnswerLine().All(c => c == 'K');

    private static Tree Rerun(Script script, Quirks quirks) =>
        new Emulator(new EmulatorOptions(quirks)).Run(script).Final;

    private static bool ExceedsInstructionBudget(Script script)
    {
        try
        {
            var options = new EmulatorOptions(DetectCycles: false, InstructionBudget: MaxBruteForceInstructions);
            new Emulator(options).Run(script);
            return false;
        }
        catch (InstructionBudgetExceededException)
        {
            return true;
        }
    }

    private static bool Contains<T>(IReadOnlyList<Instruction> body) where T : Instruction =>
        body.Any(i => i is T || (i is Repeat r && Contains<T>(r.Body)));

    private static bool SwapLeavesAnswerUnchanged(Script script, Tree answer)
    {
        var anySwap = false;
        foreach (var body in SwappedBodies(script.Body))
        {
            anySwap = true;
            if (Similarity(answer, new Emulator().Run(script with { Body = body }).Final) < 1.0) return false;
        }
        return anySwap;
    }

    private static IEnumerable<IReadOnlyList<Instruction>> SwappedBodies(IReadOnlyList<Instruction> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (i + 1 < body.Count && IsTransform(body[i]) && IsTransform(body[i + 1])
                && body[i].GetType() != body[i + 1].GetType())
            {
                var swapped = body.ToList();
                (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]);
                yield return swapped;
            }
            if (body[i] is Repeat repeat)
            {
                foreach (var inner in SwappedBodies(repeat.Body))
                {
                    var replaced = body.ToList();
                    replaced[i] = repeat with { Body = inner };
                    yield return replaced;
                }
            }
        }
    }

    private static bool IsTransform(Instruction i) => i is Rotate or Mirror;
}
