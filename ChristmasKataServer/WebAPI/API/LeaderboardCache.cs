using WebAPI.Service;

namespace WebAPI.API;

public sealed class LeaderboardCache(IParticipantRepository repository)
{
    private sealed record Snapshot(List<LeaderboardEntry> Board, List<LadderRung> Ladder);

    private readonly object _gate = new();
    private Task<Snapshot>? _current;

    public async Task<List<LeaderboardEntry>> Public() => (await Current()).Board;

    public async Task<List<LadderRung>> Ladder() => (await Current()).Ladder;

    public void Invalidate()
    {
        lock (_gate) _current = null;
    }

    private Task<Snapshot> Current()
    {
        lock (_gate)
        {
            if (_current is null || _current.IsFaulted || _current.IsCanceled) _current = Build();
            return _current;
        }
    }

    private async Task<Snapshot> Build()
    {
        var scores = (await repository.GetScores()).ToList();
        return new Snapshot(Leaderboard.Build(scores), API.Ladder.Build(scores));
    }
}
