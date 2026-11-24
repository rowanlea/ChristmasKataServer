using WebAPI.API;
using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Service;

public class LadderTests
{
    private static Participant Team(string name, params (string Puzzle, int Correct)[] scores)
    {
        var p = new Participant { Id = name, TeamName = name, UserSecret = "s" };
        foreach (var (puzzle, correct) in scores) p.PublicBestCorrect[puzzle] = correct;
        return p;
    }

    private static LadderRung Rung(List<LadderRung> ladder, string level) => ladder.Single(r => r.Level == level);

    [Test]
    public void Has_santa_then_every_puzzle_from_the_top_down()
    {
        var ladder = Ladder.Build([]);

        Assert.That(ladder.Select(r => r.Level),
            Is.EqualTo(new[] { Ladder.Santa }.Concat(PuzzleCatalogue.All.Reverse().Select(p => p.Id))));
        Assert.That(ladder[0].Name, Is.EqualTo("Found Santa"));
        Assert.That(Rung(ladder, "1.1").Name, Is.EqualTo("Paint by Numbers"));
        Assert.That(ladder.All(r => r.Teams.Count == 0), Is.True);
    }

    [Test]
    public void A_team_with_no_scores_sits_on_the_first_level()
    {
        var ladder = Ladder.Build([Team("a")]);

        Assert.That(Rung(ladder, "1.1").Teams, Is.EqualTo(new[] { new LadderTeam("a", 0) }));
    }

    [Test]
    public void A_team_sits_on_the_first_level_below_80_percent_with_its_progress_there()
    {
        // 1.1 has 136 bulbs: 108 is 79%, 109 is 80%.
        var below = Ladder.Build([Team("a", ("1.1", 108))]);
        var at = Ladder.Build([Team("a", ("1.1", 109), ("1.2", 68))]);

        Assert.That(Rung(below, "1.1").Teams, Is.EqualTo(new[] { new LadderTeam("a", 79) }));
        Assert.That(Rung(at, "1.2").Teams, Is.EqualTo(new[] { new LadderTeam("a", 50) }));
    }

    [Test]
    public void A_team_that_passes_secret_santa_sits_on_found_santa()
    {
        var all = PuzzleCatalogue.All.Select(p => (p.Id, p.Size * (p.Size + 1) / 2)).ToArray();

        var ladder = Ladder.Build([Team("a", all)]);

        Assert.That(ladder[0].Teams, Is.EqualTo(new[] { new LadderTeam("a", 100) }));
        Assert.That(ladder.Sum(r => r.Teams.Count), Is.EqualTo(1));
    }

    [Test]
    public void Teams_on_a_level_are_ordered_by_progress_then_name()
    {
        var ladder = Ladder.Build([Team("c", ("1.1", 50)), Team("b", ("1.1", 100)), Team("a", ("1.1", 100))]);

        Assert.That(Rung(ladder, "1.1").Teams.Select(t => t.TeamName), Is.EqualTo(new[] { "a", "b", "c" }));
    }
}
