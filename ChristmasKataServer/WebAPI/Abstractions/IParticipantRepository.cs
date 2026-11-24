using WebAPI.Kata.Puzzles;

namespace WebAPI.Service;

public interface IParticipantRepository
{
    Task<IEnumerable<Participant>> GetAllParticipants();
    Task<Participant?> GetParticipantBy(string teamName);
    Task<string> Create(string teamName);
    Task<bool> ParticipantExists(string teamName, string teamSecret);
    Task<(Participant, ScoreUpdate)> RecordScore(string teamName, Puzzle puzzle, int correct, bool frozen);
    Task<bool> SaveShow(string teamName, Show show);
}
