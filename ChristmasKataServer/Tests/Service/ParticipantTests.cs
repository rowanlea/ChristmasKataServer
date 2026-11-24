using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Service;

public class ParticipantTests
{
    private static Puzzle P(string id) => PuzzleCatalogue.Find(id)!;
    private static Participant New() => new() { Id = "t", TeamName = "t", UserSecret = "s" };

    [Test]
    public void RecordScore_keeps_best()
    {
        var p = New();
        p.RecordScore(P("1.1"), 130);
        var update = p.RecordScore(P("1.1"), 120);

        Assert.That(p.BestCorrect["1.1"], Is.EqualTo(130));
        Assert.That(update.BestPoints, Is.EqualTo(Points(130)));
    }

    private static int Points(int correct) => WebAPI.Scoring.Points.For(correct, 136);

    [Test]
    public void RecordScore_reports_unlock_once()
    {
        var p = New();
        Assert.That(p.RecordScore(P("1.1"), 100).Unlocked, Is.Null);
        Assert.That(p.RecordScore(P("1.1"), 120).Unlocked?.Id, Is.EqualTo("1.2"));
        Assert.That(p.RecordScore(P("1.1"), 136).Unlocked, Is.Null);
    }

    [Test]
    public void IsUnlocked_follows_previous_puzzle()
    {
        var p = New();
        Assert.That(p.IsUnlocked(P("1.1")), Is.True);
        Assert.That(p.IsUnlocked(P("1.2")), Is.False);

        p.RecordScore(P("1.1"), 120);

        Assert.That(p.IsUnlocked(P("1.2")), Is.True);
        Assert.That(p.IsUnlocked(P("1.3")), Is.False);
    }

    [Test]
    public void MayShow_needs_full_marks_on_secret_santa()
    {
        var p = New();
        p.RecordScore(P("7.1"), 1175);
        Assert.That(p.MayShow, Is.False);
        p.RecordScore(P("7.1"), 1176);
        Assert.That(p.MayShow, Is.True);
    }

    [Test]
    public void Legacy_document_with_PublicBestCorrect_still_loads()
    {
        var json = """{"id":"t","teamName":"t","BestCorrect":{"1.1":136},"PublicBestCorrect":{"1.1":100}}""";

        var p = Newtonsoft.Json.JsonConvert.DeserializeObject<Participant>(json)!;

        Assert.That(p.BestCorrect["1.1"], Is.EqualTo(136));
    }
}
