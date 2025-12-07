using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WebAPI.Service;

public class ParticipantRepository : IParticipantRepository
{
    private readonly ConcurrentDictionary<string, Participant> _participants = new();

    public Participant TryGetOrCreateParticipant(string teamName, string secret)
    {
        var participant = _participants.GetOrAdd(teamName, _ => new Participant
        {
            TeamName = teamName,
            UserSecret = secret
        });

        // Validate the secret matches (whether newly created or existing)
        if (participant.UserSecret != secret)
        {
            throw new UnauthorizedAccessException("Team name exists but secret does not match");
        }

        return participant;
    }

    public IEnumerable<Participant> GetAllParticipants()
    {
        return _participants.Values;
    }

    public Participant? GetParticipantBy(string teamName)
    {
        _participants.TryGetValue(teamName, out var participant);
        return participant;
    }

    public string Create(string teamName)
    {
        var userSecret = $"{teamName}-secret-${RandomNumberGenerator.GetInt32(1000, 9999)}";
        var isSuccessful = _participants.TryAdd(teamName, new Participant
        {
            TeamName = teamName,
            UserSecret = userSecret,
        });

        return isSuccessful ? userSecret : string.Empty;
    }

    public Task<bool> ParticipantExists(string teamName, string teamSecret)
    {
        var teamExists = _participants.TryGetValue(teamName, out var participant);
        if (!teamExists)
        {
            return Task.FromResult(false);
        }
        
        return participant!.UserSecret == teamSecret ?  Task.FromResult(true) : Task.FromResult(false);
    }
}

