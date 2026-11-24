using System.Collections.Concurrent;
using WebAPI.Kata.Puzzles;

namespace WebAPI.Service;

public class ParticipantRepository : IParticipantRepository
{
    private readonly ConcurrentDictionary<string, Participant> _participants = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public Task<IEnumerable<Participant>> GetAllParticipants()
    {
        return Task.FromResult<IEnumerable<Participant>>(_participants.Keys.Select(Snapshot).OfType<Participant>().ToList());
    }

    public Task<IEnumerable<Participant>> GetScores()
    {
        return Task.FromResult<IEnumerable<Participant>>(_participants.Keys.Select(Snapshot).OfType<Participant>()
            .Select(p => new Participant
            {
                Id = p.Id,
                TeamName = p.TeamName,
                BestCorrect = p.BestCorrect,
            })
            .ToList());
    }

    public Task<Participant?> GetParticipantBy(string teamName)
    {
        return Task.FromResult(Snapshot(teamName));
    }

    public Task<string?> Create(string teamName)
    {
        var userSecret = TeamSecrets.New();
        var isSuccessful = _participants.TryAdd(teamName, new Participant
        {
            Id = teamName,
            TeamName = teamName,
            UserSecret = userSecret,
        });

        return Task.FromResult(isSuccessful ? userSecret : null);
    }

    public Task<bool> ParticipantExists(string teamName, string teamSecret)
    {
        var teamExists = _participants.TryGetValue(teamName, out var participant);
        if (!teamExists)
        {
            return Task.FromResult(false);
        }
        
        return Task.FromResult(participant!.UserSecret == teamSecret);
    }

    public Task<(Participant, ScoreUpdate)> RecordScore(string teamName, Puzzle puzzle, int correct)
    {
        var participant = Require(teamName);
        lock (_locks.GetOrAdd(teamName, _ => new object()))
        {
            var update = participant.RecordScore(puzzle, correct);
            return Task.FromResult((Copy(participant), update));
        }
    }

    public Task<bool> SaveShow(string teamName, Show show)
    {
        var participant = Require(teamName);
        lock (_locks.GetOrAdd(teamName, _ => new object()))
        {
            var first = participant.Show is null;
            participant.Show = show;
            return Task.FromResult(first);
        }
    }

    private Participant? Snapshot(string teamName)
    {
        if (!_participants.TryGetValue(teamName, out var participant)) return null;
        lock (_locks.GetOrAdd(teamName, _ => new object()))
        {
            return Copy(participant);
        }
    }

    private static Participant Copy(Participant p) => p with
    {
        BestCorrect = new Dictionary<string, int>(p.BestCorrect),
    };

    private Participant Require(string teamName) =>
        _participants.TryGetValue(teamName, out var p) ? p : throw new KeyNotFoundException($"Unknown team {teamName}");
}
