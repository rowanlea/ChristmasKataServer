namespace WebAPI.Service;

public interface IParticipantRepository
{
    Participant TryGetOrCreateParticipant(string teamName, string secret);
    IEnumerable<Participant> GetAllParticipants();
    Participant? GetParticipantBy(string teamName);
    string Create(string teamName);
    Task<bool> ParticipantExists(string teamName, string teamSecret);
}