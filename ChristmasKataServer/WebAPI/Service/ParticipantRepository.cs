using System.Collections.Concurrent;

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
}

