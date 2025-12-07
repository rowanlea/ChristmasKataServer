using System.Collections.Concurrent;

namespace WebAPI.Service;

public class ParticipantRepository : IParticipantRepository
{
    private readonly ConcurrentDictionary<string, Participant> _participants = new();

    public Participant GetParticipantScore(string teamName)
    {
        return _participants.GetOrAdd(teamName, _ => new Participant
        {
            TeamName = teamName,
            UserSecret = string.Empty
        });
    }
}

