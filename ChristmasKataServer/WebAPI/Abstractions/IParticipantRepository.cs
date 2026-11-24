using WebAPI.Kata.Puzzles;

namespace WebAPI.Service;

public interface IParticipantRepository
{
    Task<IEnumerable<Participant>> GetAllParticipants();

    /// <returns>Every team with only its id, team name and best scores: no secret and no show.</returns>
    Task<IEnumerable<Participant>> GetScores();
    Task<Participant?> GetParticipantBy(string teamName);
    /// <returns>The new team secret, or null when the name is already taken.</returns>
    Task<string?> Create(string teamName);
    Task<bool> ParticipantExists(string teamName, string teamSecret);
    Task<(Participant, ScoreUpdate)> RecordScore(string teamName, Puzzle puzzle, int correct, bool frozen);
    Task<bool> SaveShow(string teamName, Show show);
}
