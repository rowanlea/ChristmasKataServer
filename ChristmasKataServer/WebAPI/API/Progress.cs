using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;
using WebAPI.Service;

namespace WebAPI.API;

public sealed record PuzzleProgress(string Id, string Status, int? Percent);

public sealed record TeamProgress(string TeamName, string Next, bool ShowsUnlocked, List<PuzzleProgress> Puzzles);

public static class Progress
{
    public static TeamProgress Build(Participant participant)
    {
        var puzzles = PuzzleCatalogue.All.Select(puzzle =>
        {
            var bulbs = puzzle.Size * (puzzle.Size + 1) / 2;
            int? correct = participant.BestCorrect.TryGetValue(puzzle.Id, out var best) ? best : null;
            var status = correct is { } c && Points.Passes(c, bulbs) ? "passed"
                : participant.IsUnlocked(puzzle) ? "current"
                : "locked";
            int? percent = correct is { } n ? (int)Math.Floor(100.0 * n / bulbs) : null;
            return new PuzzleProgress(puzzle.Id, status, percent);
        }).ToList();

        var next = puzzles.FirstOrDefault(p => p.Status != "passed")?.Id ?? Ladder.Santa;
        return new TeamProgress(participant.TeamName, next, participant.MayShow, puzzles);
    }
}
