namespace WebAPI.Service;

public interface IParticipantRepository
{
    Task<IEnumerable<Participant>> GetAllParticipants();
    Task<Participant?> GetParticipantBy(string teamName);
    Task<string> Create(string teamName);
    Task<bool> ParticipantExists(string teamName, string teamSecret);
    Task UpdateParticipantScore(Participant participant);
}