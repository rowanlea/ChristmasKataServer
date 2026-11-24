using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Api;

public sealed class SpyRepository(IParticipantRepository inner) : IParticipantRepository
{
    private int _allTeamReads;

    public bool HideOnLookup { get; set; }

    public int AllTeamReads => Volatile.Read(ref _allTeamReads);

    public Task<IEnumerable<Participant>> GetAllParticipants()
    {
        Interlocked.Increment(ref _allTeamReads);
        return inner.GetAllParticipants();
    }

    public Task<IEnumerable<Participant>> GetScores()
    {
        Interlocked.Increment(ref _allTeamReads);
        return inner.GetScores();
    }

    public async Task<Participant?> GetParticipantBy(string teamName) =>
        HideOnLookup ? null : await inner.GetParticipantBy(teamName);

    public Task<string?> Create(string teamName) => inner.Create(teamName);

    public Task<bool> ParticipantExists(string teamName, string teamSecret) => inner.ParticipantExists(teamName, teamSecret);

    public Task<(Participant, ScoreUpdate)> RecordScore(string teamName, Puzzle puzzle, int correct, bool frozen) =>
        inner.RecordScore(teamName, puzzle, correct, frozen);

    public Task<bool> SaveShow(string teamName, Show show) => inner.SaveShow(teamName, show);
}
