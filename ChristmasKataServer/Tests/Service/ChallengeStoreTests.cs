using Microsoft.Extensions.Time.Testing;
using WebAPI.Kata;
using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Service;

public class ChallengeStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 12, 10, 18, 0, 0, TimeSpan.Zero));
    private ChallengeStore _store = null!;
    private readonly Puzzle _puzzle = PuzzleCatalogue.Find("1.1")!;

    [SetUp]
    public void SetUp() => _store = new ChallengeStore(_time);

    private Challenge Issue(string team) => _store.Issue(team, _puzzle, new GeneratedChallenge("SIZE 16", new Tree(16)));

    [Test]
    public void Take_returns_the_challenge_once()
    {
        var c = Issue("a");
        var (outcome, taken) = _store.Take(c.Id, "a");

        Assert.That(outcome, Is.EqualTo(TakeOutcome.Ok));
        Assert.That(taken, Is.EqualTo(c));
        Assert.That(c.ExpiresAt, Is.EqualTo(_time.GetUtcNow() + TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void Expired_challenge_stays_Expired_after_another_team_issues()
    {
        var c = Issue("a");
        _time.Advance(TimeSpan.FromMinutes(6));
        Issue("b");

        Assert.That(_store.Take(c.Id, "a").Item1, Is.EqualTo(TakeOutcome.Expired));
    }

    [Test]
    public void Long_expired_challenges_are_purged()
    {
        var c = Issue("a");
        _time.Advance(TimeSpan.FromHours(2));
        Issue("b");

        Assert.That(_store.Take(c.Id, "a").Item1, Is.EqualTo(TakeOutcome.NotFound));
    }

    [Test]
    public void Take_twice_is_AlreadySubmitted()
    {
        var c = Issue("a");
        _store.Take(c.Id, "a");
        Assert.That(_store.Take(c.Id, "a").Item1, Is.EqualTo(TakeOutcome.AlreadySubmitted));
    }

    [Test]
    public void Take_by_other_team_is_NotFound()
    {
        var c = Issue("a");
        Assert.That(_store.Take(c.Id, "b").Item1, Is.EqualTo(TakeOutcome.NotFound));
        Assert.That(_store.Take(c.Id, "a").Item1, Is.EqualTo(TakeOutcome.Ok));
    }

    [Test]
    public void Take_unknown_is_NotFound() =>
        Assert.That(_store.Take(Guid.NewGuid(), "a").Item1, Is.EqualTo(TakeOutcome.NotFound));

    [Test]
    public void Take_after_5_minutes_is_Expired()
    {
        var c = Issue("a");
        _time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.That(_store.Take(c.Id, "a").Item1, Is.EqualTo(TakeOutcome.Expired));
    }

    [Test]
    public void Concurrent_take_succeeds_once()
    {
        var c = Issue("a");
        var outcomes = new System.Collections.Concurrent.ConcurrentBag<TakeOutcome>();
        Parallel.For(0, 50, _ => outcomes.Add(_store.Take(c.Id, "a").Item1));
        Assert.That(outcomes.Count(o => o == TakeOutcome.Ok), Is.EqualTo(1));
    }
}
