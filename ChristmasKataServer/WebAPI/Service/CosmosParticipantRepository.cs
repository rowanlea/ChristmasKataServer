using System.Net;
using Microsoft.Azure.Cosmos;
using WebAPI.Database;
using WebAPI.Kata.Puzzles;

namespace WebAPI.Service;

public class CosmosParticipantRepository : IParticipantRepository
{
    private readonly Container _container;
    
    public CosmosParticipantRepository(ICosmosDbClient client)
    {
        _container = client.Container;
    }
    public Task<IEnumerable<Participant>> GetAllParticipants() => Query("SELECT * FROM c");

    public Task<IEnumerable<Participant>> GetScores() =>
        Query("SELECT c.id, c.teamName, c.BestCorrect, c.PublicBestCorrect FROM c");

    private async Task<IEnumerable<Participant>> Query(string query)
    {
        var queryDefinition = new QueryDefinition(query);
        var result = _container.GetItemQueryIterator<Participant>(queryDefinition);
        
        var results = new List<Participant>();
        while (result.HasMoreResults)
        {
            results.AddRange(await result.ReadNextAsync());
        }

        return results;
    }

    public async Task<Participant?> GetParticipantBy(string teamName)
    {
        // Header values reach this point unchecked, and ids may not contain '/', '\', '?' or '#'.
        if (!TeamNames.IsValid(teamName)) return null;
        try
        {
            var read = await _container.ReadItemAsync<Participant>(teamName, new PartitionKey(teamName));
            return read.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<string?> Create(string teamName)
    {
        var userSecret = TeamSecrets.New();
        var participant = new Participant
        {
            Id = teamName, 
            TeamName = teamName,
            UserSecret = userSecret,
        };

        try
        {
            var result = await _container.CreateItemAsync(participant, new PartitionKey(teamName));
            return result.Resource.UserSecret;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }
    }

    public async Task<bool> ParticipantExists(string teamName, string teamSecret)
    {
        var participant = await GetParticipantBy(teamName);
        return participant != null && participant.UserSecret == teamSecret;
    }

    public Task EnsureCreated() =>
        _container.Database.CreateContainerIfNotExistsAsync(new ContainerProperties(_container.Id, "/id"));

    public async Task<(Participant, ScoreUpdate)> RecordScore(string teamName, Puzzle puzzle, int correct, bool frozen)
    {
        ScoreUpdate? update = null;
        var participant = await ReadModifyReplace(teamName, p => update = p.RecordScore(puzzle, correct, frozen));
        return (participant, update!);
    }

    public async Task<bool> SaveShow(string teamName, Show show)
    {
        var first = false;
        await ReadModifyReplace(teamName, p =>
        {
            first = p.Show is null;
            p.Show = show;
        });
        return first;
    }

    private async Task<Participant> ReadModifyReplace(string teamName, Action<Participant> modify)
    {
        const int attempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var key = new PartitionKey(teamName);
            var read = await _container.ReadItemAsync<Participant>(teamName, key);
            var participant = read.Resource;
            modify(participant);
            try
            {
                var written = await _container.ReplaceItemAsync(participant, teamName, key,
                    new ItemRequestOptions { IfMatchEtag = read.ETag });
                return written.Resource;
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.PreconditionFailed && attempt < attempts)
            {
            }
        }
    }
}
