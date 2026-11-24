using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Service;

public class FeedTests
{
    private static Feed NewFeed() => new();

    private static Puzzle P(string id) => PuzzleCatalogue.Find(id)!;

    private static int Full(string id) => P(id).Size * (P(id).Size + 1) / 2;

    private static Tile Solve(Feed feed, string team, string id) => feed.Publish(team, P(id), Full(id), "R");

    private static List<string> Round1() => PuzzleCatalogue.InRound(1).Select(p => p.Id).ToList();

    [Test]
    public void First_full_marks_gets_banner_second_does_not()
    {
        var feed = NewFeed();

        var first = Solve(feed, "alpha", "1.1");
        var second = Solve(feed, "beta", "1.1");

        Assert.That(first.Banner, Is.EqualTo(new Banner("first-full-marks", "alpha first to solve 1.1 Paint by Numbers")));
        Assert.That(second.Banner, Is.Null);
    }

    [Test]
    public void Partial_marks_get_no_banner()
    {
        var tile = NewFeed().Publish("alpha", P("1.1"), Full("1.1") - 1, "R");

        Assert.That(tile.Banner, Is.Null);
    }

    [Test]
    public void Secret_santa_every_solve_gets_banner()
    {
        var feed = NewFeed();

        var first = Solve(feed, "alpha", "7.1");
        var second = Solve(feed, "beta", "7.1");

        Assert.That(first.Banner, Is.EqualTo(new Banner("secret-santa", "alpha found Santa!")));
        Assert.That(second.Banner, Is.EqualTo(new Banner("secret-santa", "beta found Santa!")));
    }

    [Test]
    public void Round_finish_banner_once()
    {
        var feed = NewFeed();
        var ids = Round1();
        foreach (var id in ids[..^1]) Solve(feed, "beta", id);
        Solve(feed, "alpha", ids[^1]);

        var alphaFinish = ids[..^1].Select(id => Solve(feed, "alpha", id)).ToList().Last();
        var betaFinish = Solve(feed, "beta", ids[^1]);

        Assert.That(alphaFinish.Banner, Is.EqualTo(new Banner("first-round-finish", "alpha first to finish round 1")));
        Assert.That(betaFinish.Banner, Is.Null);
    }

    [Test]
    public void Round_finish_beats_puzzle_first_on_same_tile()
    {
        var feed = NewFeed();
        var ids = Round1();
        foreach (var id in ids[..^1]) Solve(feed, "alpha", id);

        var finish = Solve(feed, "alpha", ids[^1]);
        var later = Solve(feed, "beta", ids[^1]);

        Assert.That(finish.Banner, Is.EqualTo(new Banner("first-round-finish", "alpha first to finish round 1")));
        Assert.That(later.Banner, Is.Null);
    }

    [Test]
    public void Feed_keeps_latest_eight_newest_first()
    {
        var feed = NewFeed();

        for (var i = 0; i < 10; i++) feed.Publish($"team-{i}", P("1.1"), 1, "R");

        Assert.That(feed.Latest.Select(t => t.TeamName),
            Is.EqualTo(Enumerable.Range(2, 8).Reverse().Select(i => $"team-{i}")));
    }

    [Test]
    public async Task Seeded_firsts_survive_restart()
    {
        var repo = new ParticipantRepository();
        await repo.Create("veteran");
        await repo.RecordScore("veteran", P("1.1"), Full("1.1"));
        var feed = NewFeed();

        await feed.InitialiseAsync(repo);

        Assert.That(Solve(feed, "newcomer", "1.1").Banner, Is.Null);
    }

    [Test]
    public async Task Seeded_round_finish_survives_restart()
    {
        var repo = new ParticipantRepository();
        await repo.Create("veteran");
        var ids = Round1();
        foreach (var id in ids) await repo.RecordScore("veteran", P(id), Full(id));
        var feed = NewFeed();
        await feed.InitialiseAsync(repo);

        // Puzzle firsts are seeded too, so only the round banner could appear; it must not.
        var tiles = ids.Select(id => Solve(feed, "newcomer", id)).ToList();

        Assert.That(tiles.Select(t => t.Banner), Is.All.Null);
    }

    [Test]
    public void Tile_always_carries_correct_and_banner()
    {
        var tile = NewFeed().Publish("alpha", P("1.1"), Full("1.1"), "R");

        Assert.That(tile.Correct, Is.EqualTo(Full("1.1")));
        Assert.That(tile.Banner, Is.Not.Null);
    }

    [Test]
    public async Task Every_score_broadcasts_LeaderboardChanged()
    {
        var feed = NewFeed();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reader = feed.Subscribe(cts.Token);

        feed.Publish("alpha", P("1.1"), 5, "R");
        feed.Publish("beta", P("1.1"), 6, "R");

        var events = new[]
        {
            await reader.ReadAsync(cts.Token), await reader.ReadAsync(cts.Token),
            await reader.ReadAsync(cts.Token), await reader.ReadAsync(cts.Token),
        };
        Assert.That(events.OfType<LeaderboardChanged>().Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Subscriber_receives_tile_and_show()
    {
        var feed = NewFeed();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reader = feed.Subscribe(cts.Token);

        var tile = feed.Publish("alpha", P("1.1"), 5, "R");
        feed.PublishShow("alpha", true);

        Assert.That(await reader.ReadAsync(cts.Token), Is.EqualTo(new TileAdded(tile)));
        Assert.That(await reader.ReadAsync(cts.Token), Is.EqualTo(new LeaderboardChanged()));
        Assert.That(await reader.ReadAsync(cts.Token), Is.EqualTo(new ShowAdded("alpha", true)));
    }

    [Test]
    public async Task Slow_subscriber_drops_oldest_and_cancel_completes()
    {
        var feed = NewFeed();
        using var cts = new CancellationTokenSource();
        var reader = feed.Subscribe(cts.Token);
        for (var i = 0; i < 200; i++) feed.PublishShow($"t{i}", false);

        var first = (ShowAdded)await reader.ReadAsync();
        cts.Cancel();

        Assert.That(first.TeamName, Is.Not.EqualTo("t0"));
        while (reader.TryRead(out _)) { }
        await reader.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
