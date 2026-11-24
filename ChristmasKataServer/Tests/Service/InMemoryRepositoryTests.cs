using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Service;

public class InMemoryRepositoryTests
{
    [Test]
    public async Task Concurrent_record_keeps_best()
    {
        var repo = new ParticipantRepository();
        await repo.Create("team");
        var puzzle = PuzzleCatalogue.Find("1.1")!;

        await Task.WhenAll(Enumerable.Range(1, 50)
            .Select(n => Task.Run(() => repo.RecordScore("team", puzzle, n, false))));

        var p = await repo.GetParticipantBy("team");
        Assert.That(p!.BestCorrect["1.1"], Is.EqualTo(50));
    }

    [Test]
    public async Task SaveShow_replaces_and_reports_first()
    {
        var repo = new ParticipantRepository();
        await repo.Create("team");
        var at = new DateTimeOffset(2026, 12, 10, 19, 0, 0, TimeSpan.Zero);

        var first = await repo.SaveShow("team", new Show(["A"], at));
        var second = await repo.SaveShow("team", new Show(["B"], at.AddMinutes(1)));

        Assert.That(first, Is.True);
        Assert.That(second, Is.False);
        Assert.That((await repo.GetParticipantBy("team"))!.Show!.Frames, Is.EqualTo(new[] { "B" }));
    }
}
