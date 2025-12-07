namespace WebAPI.Service;

public interface IParticipantRepository
{
    Participant TryGetOrCreateParticipant(string teamName, string secret);
    IEnumerable<Participant> GetAllParticipants();
}