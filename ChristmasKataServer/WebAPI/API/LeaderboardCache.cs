using WebAPI.Service;

namespace WebAPI.API;

public sealed class LeaderboardCache(IParticipantRepository repository)
{
    private readonly object _gate = new();
    private Task<List<LeaderboardEntry>>? _current;

    public Task<List<LeaderboardEntry>> Public()
    {
        lock (_gate)
        {
            if (_current is null || _current.IsFaulted || _current.IsCanceled) _current = Build();
            return _current;
        }
    }

    public void Invalidate()
    {
        lock (_gate) _current = null;
    }

    private async Task<List<LeaderboardEntry>> Build() =>
        Leaderboard.Build(await repository.GetScores(), live: false);
}
