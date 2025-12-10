namespace WebAPI.Service;

public interface IParticipantRepository
{
    Participant TryGetOrCreateParticipant(string teamName, string secret);
    IEnumerable<Participant> GetAllParticipants();
    Task<Participant?> GetParticipantBy(string teamName);
    Task<string> Create(string teamName);
    Task<bool> ParticipantExists(string teamName, string teamSecret);
    Task UpdateParticipantScore(Participant participant);
}