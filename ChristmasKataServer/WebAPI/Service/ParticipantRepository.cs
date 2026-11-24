using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WebAPI.Service;

public class ParticipantRepository : IParticipantRepository
{
    private readonly ConcurrentDictionary<string, Participant> _participants = new();

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

    public Task UpdateParticipantScore(Participant participant)
    {
        _participants[participant.TeamName] = participant;
        return Task.CompletedTask;
    }
}

