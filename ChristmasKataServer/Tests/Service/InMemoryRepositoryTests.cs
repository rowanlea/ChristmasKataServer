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

    [Test]
    public async Task Reads_are_snapshots_safe_against_concurrent_writes()
    {
        var repo = new ParticipantRepository();
        await repo.Create("team");
        var (held, _) = await repo.RecordScore("team", PuzzleCatalogue.All[0], 1, false);
        var heldCount = held.BestCorrect.Count;
        var done = false;

        var reader = Task.Run(async () =>
        {
            while (!Volatile.Read(ref done))
                foreach (var p in await repo.GetAllParticipants())
                    foreach (var _ in p.BestCorrect) { }
        });
        var writers = Task.WhenAll(Enumerable.Range(0, 4).Select(w => Task.Run(async () =>
        {
            for (var i = 0; i < 200; i++)
                foreach (var puzzle in PuzzleCatalogue.All)
                    await repo.RecordScore("team", puzzle, i + w, i % 2 == 0);
        })));

        await writers;
        Volatile.Write(ref done, true);
        await reader;

        Assert.That(held.BestCorrect.Count, Is.EqualTo(heldCount));
        Assert.That(held.BestCorrect["1.1"], Is.EqualTo(1));
        var now = await repo.GetParticipantBy("team");
        Assert.That(now!.BestCorrect.Count, Is.EqualTo(PuzzleCatalogue.All.Count));
    }

    [Test]
    public async Task Create_on_a_taken_name_returns_null_and_keeps_the_first_secret()
    {
        var repo = new ParticipantRepository();
        var first = await repo.Create("team");

        var second = await repo.Create("team");

        Assert.That(second, Is.Null);
        Assert.That(await repo.ParticipantExists("team", first!), Is.True);
    }
}
