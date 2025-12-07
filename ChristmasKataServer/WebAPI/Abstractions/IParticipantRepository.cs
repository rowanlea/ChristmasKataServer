namespace WebAPI.Service;

public interface IParticipantRepository
{
    Participant GetParticipantScore(string teamName);
}