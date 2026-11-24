using WebAPI.API;
using WebAPI.Service;

namespace Tests.Service;

public class LeaderboardTests
{
    private static Participant Team(string name, params (string Puzzle, int Correct)[] scores)
    {
        var p = new Participant { Id = name, TeamName = name, UserSecret = "s" };
        foreach (var (puzzle, correct) in scores) p.BestCorrect[puzzle] = correct;
        return p;
    }

    [Test]
    public void Tied_totals_share_a_rank_and_the_next_rank_skips()
    {
        var participants = new[]
        {
            Team("b", ("1.1", 136), ("1.2", 136)),
            Team("a", ("1.1", 136), ("1.2", 136)),
            Team("c", ("1.1", 136)),
        };

        var ranks = Leaderboard.Build(participants).Select(e => (e.Rank, e.TeamName));

        Assert.That(ranks, Is.EqualTo(new[] { (1, "a"), (1, "b"), (3, "c") }));
    }
}
