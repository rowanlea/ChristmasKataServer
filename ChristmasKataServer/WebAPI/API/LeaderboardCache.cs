using System.Security.Cryptography;
using System.Text.Json;
using WebAPI.Service;

namespace WebAPI.API;

public sealed record LeaderboardSnapshot(List<LeaderboardEntry> Teams, List<LadderRung> Ladder);

public sealed record CachedLeaderboard(byte[] Json, string ETag);

public sealed class LeaderboardCache(IParticipantRepository repository)
{
    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private Task<CachedLeaderboard>? _current;

    public void Invalidate()
    {
        lock (_gate) _current = null;
    }

    public Task<CachedLeaderboard> Current()
    {
        lock (_gate)
        {
            if (_current is null || _current.IsFaulted || _current.IsCanceled) _current = Build();
            return _current;
        }
    }

    private async Task<CachedLeaderboard> Build()
    {
        var scores = (await repository.GetScores()).ToList();
        var snapshot = new LeaderboardSnapshot(Leaderboard.Build(scores), Ladder.Build(scores));
        var json = JsonSerializer.SerializeToUtf8Bytes(snapshot, Compact);
        return new CachedLeaderboard(json, "\"" + Convert.ToHexString(SHA256.HashData(json))[..16] + "\"");
    }
}
