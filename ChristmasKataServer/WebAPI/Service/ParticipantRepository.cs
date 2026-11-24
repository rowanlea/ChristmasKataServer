using System.Collections.Concurrent;
using System.Security.Cryptography;
using WebAPI.Kata.Puzzles;

namespace WebAPI.Service;

public class ParticipantRepository : IParticipantRepository
{
    private readonly ConcurrentDictionary<string, Participant> _participants = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public Task<IEnumerable<Participant>> GetAllParticipants()
    {
        return Task.FromResult<IEnumerable<Participant>>(_participants.Values.ToList());
    }

    public Task<Participant?> GetParticipantBy(string teamName)
    {
        _participants.TryGetValue(teamName, out var participant);
        return Task.FromResult(participant);
    }

    public Task<string> Create(string teamName)
    {
        var userSecret = $"{teamName}-secret-${RandomNumberGenerator.GetInt32(1000, 9999)}";
        var isSuccessful = _participants.TryAdd(teamName, new Participant
        {
            Id = teamName,
            TeamName = teamName,
            UserSecret = userSecret,
        });

        return Task.FromResult(isSuccessful ? userSecret : string.Empty);
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

    public Task<(Participant, ScoreUpdate)> RecordScore(string teamName, Puzzle puzzle, int correct, bool frozen)
    {
        var participant = Require(teamName);
        lock (_locks.GetOrAdd(teamName, _ => new object()))
        {
            var update = participant.RecordScore(puzzle, correct, frozen);
            return Task.FromResult((participant, update));
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

    private Participant Require(string teamName) =>
        _participants.TryGetValue(teamName, out var p) ? p : throw new KeyNotFoundException($"Unknown team {teamName}");
}
