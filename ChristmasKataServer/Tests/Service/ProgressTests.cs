using WebAPI.API;
using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Service;

public class ProgressTests
{
    private static int Bulbs(string id)
    {
        var size = PuzzleCatalogue.Find(id)!.Size;
        return size * (size + 1) / 2;
    }

    private static Participant Team(params (string Puzzle, int Correct)[] scores)
    {
        var p = new Participant { Id = "t", TeamName = "t", UserSecret = "s" };
        foreach (var (puzzle, correct) in scores) p.BestCorrect[puzzle] = correct;
        return p;
    }

    private static PuzzleProgress For(TeamProgress progress, string id) =>
        progress.Puzzles.Single(p => p.Id == id);

    [Test]
    public void New_team_is_on_1_1_with_everything_else_locked()
    {
        var progress = Progress.Build(Team());

        Assert.That(progress.TeamName, Is.EqualTo("t"));
        Assert.That(progress.Next, Is.EqualTo("1.1"));
        Assert.That(progress.ShowsUnlocked, Is.False);
        Assert.That(For(progress, "1.1").Status, Is.EqualTo("current"));
        Assert.That(progress.Puzzles.Skip(1).Select(p => p.Status), Is.All.EqualTo("locked"));
        Assert.That(progress.Puzzles.Select(p => p.Percent), Is.All.Null);
    }

    [Test]
    public void Passing_without_full_marks_unlocks_the_next_puzzle()
    {
        var pass = (int)Math.Ceiling(0.8 * Bulbs("1.1"));

        var progress = Progress.Build(Team(("1.1", pass)));

        Assert.That(For(progress, "1.1"), Is.EqualTo(new PuzzleProgress("1.1", "passed", (int)Math.Floor(100.0 * pass / Bulbs("1.1")))));
        Assert.That(For(progress, "1.2").Status, Is.EqualTo("current"));
        Assert.That(For(progress, "1.3").Status, Is.EqualTo("locked"));
        Assert.That(progress.Next, Is.EqualTo("1.2"));
    }

    [Test]
    public void Failed_attempt_shows_percent_but_stays_current()
    {
        var progress = Progress.Build(Team(("1.1", 56)));

        Assert.That(For(progress, "1.1"), Is.EqualTo(new PuzzleProgress("1.1", "current", 41)));
        Assert.That(For(progress, "1.2").Status, Is.EqualTo("locked"));
        Assert.That(progress.Next, Is.EqualTo("1.1"));
    }

    [Test]
    public void All_passed_means_next_is_santa()
    {
        var all = PuzzleCatalogue.All.Select(p => (p.Id, (int)Math.Ceiling(0.8 * Bulbs(p.Id)))).ToArray();

        var progress = Progress.Build(Team(all));

        Assert.That(progress.Puzzles.Select(p => p.Status), Is.All.EqualTo("passed"));
        Assert.That(progress.Next, Is.EqualTo(Ladder.Santa));
    }

    [Test]
    public void Full_marks_on_7_1_unlocks_shows()
    {
        Assert.That(Progress.Build(Team(("7.1", Bulbs("7.1")))).ShowsUnlocked, Is.True);
    }

    [Test]
    public void Passing_7_1_without_full_marks_does_not()
    {
        Assert.That(Progress.Build(Team(("7.1", Bulbs("7.1") - 1))).ShowsUnlocked, Is.False);
    }

    [Test]
    public void Puzzles_are_listed_in_catalogue_order()
    {
        var progress = Progress.Build(Team());

        Assert.That(progress.Puzzles.Select(p => p.Id), Is.EqualTo(PuzzleCatalogue.All.Select(p => p.Id)));
    }
}
