using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;
using WebAPI.Service;

namespace WebAPI.API;

public sealed record LadderTeam(string TeamName, int Percent);

public sealed record LadderRung(string Level, string Name, List<LadderTeam> Teams);

public static class Ladder
{
    public const string Santa = "santa";

    // Each team sits on the first level it has not yet passed, from its public best scores.
    public static List<LadderRung> Build(IEnumerable<Participant> participants)
    {
        var rungs = new List<LadderRung> { new(Santa, "Found Santa", []) };
        rungs.AddRange(PuzzleCatalogue.All.Reverse().Select(p => new LadderRung(p.Id, p.Name, [])));
        var byLevel = rungs.ToDictionary(r => r.Level);

        foreach (var participant in participants)
        {
            var (level, percent) = Place(participant);
            byLevel[level].Teams.Add(new LadderTeam(participant.TeamName, percent));
        }

        foreach (var rung in rungs)
            rung.Teams.Sort((a, b) => b.Percent != a.Percent
                ? b.Percent.CompareTo(a.Percent)
                : StringComparer.Ordinal.Compare(a.TeamName, b.TeamName));
        return rungs;
    }

    private static (string Level, int Percent) Place(Participant participant)
    {
        foreach (var puzzle in PuzzleCatalogue.All)
        {
            var bulbs = puzzle.Size * (puzzle.Size + 1) / 2;
            var correct = participant.PublicBestCorrect.GetValueOrDefault(puzzle.Id);
            if (!Points.Passes(correct, bulbs)) return (puzzle.Id, 100 * correct / bulbs);
        }
        return (Santa, 100);
    }
}
