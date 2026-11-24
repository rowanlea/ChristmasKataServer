using WebAPI.Kata;
using WebAPI.Kata.Puzzles;

namespace WebAPI.Service;

public sealed record Challenge(Guid Id, string TeamName, string Puzzle, int Size, Tree Answer, DateTimeOffset ExpiresAt);

public enum TakeOutcome { Ok, NotFound, AlreadySubmitted, Expired }

public sealed class ChallengeStore(TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private readonly Dictionary<Guid, (Challenge Challenge, bool Taken)> _entries = new();

    public Challenge Issue(string team, Puzzle p, GeneratedChallenge g)
    {
        var now = time.GetUtcNow();
        var challenge = new Challenge(Guid.NewGuid(), team, p.Id, p.Size, g.Answer, now + Lifetime);
        lock (_gate)
        {
            foreach (var id in _entries.Where(e => e.Value.Challenge.ExpiresAt <= now).Select(e => e.Key).ToList())
                _entries.Remove(id);
            _entries[challenge.Id] = (challenge, false);
        }
        return challenge;
    }

    public (TakeOutcome, Challenge?) Take(Guid id, string team)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.Challenge.TeamName != team)
                return (TakeOutcome.NotFound, null);
            if (entry.Taken)
                return (TakeOutcome.AlreadySubmitted, null);
            if (entry.Challenge.ExpiresAt <= time.GetUtcNow())
                return (TakeOutcome.Expired, null);

            _entries[id] = (entry.Challenge, true);
            return (TakeOutcome.Ok, entry.Challenge);
        }
    }
}
