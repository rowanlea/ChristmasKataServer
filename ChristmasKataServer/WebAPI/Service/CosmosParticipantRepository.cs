using System.Net;
using System.Security.Cryptography;
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
    public async Task<IEnumerable<Participant>> GetAllParticipants()
    {
        var queryDefinition = new QueryDefinition("SELECT * FROM c");
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
        var queryDefinition = new QueryDefinition(
            "SELECT * FROM c WHERE c.teamName = @teamName")
            .WithParameter("@teamName", teamName);
        var result = _container.GetItemQueryIterator<Participant>(queryDefinition);
        
        var results = new List<Participant>();
        while (result.HasMoreResults)
        {
            results.AddRange(await result.ReadNextAsync());
        }

        return results.FirstOrDefault();
    }

    public async Task<string> Create(string teamName)
    {
        var userSecret = $"{teamName}-secret-${RandomNumberGenerator.GetInt32(1000, 9999)}";
        var participant = new Participant
        {
            Id = teamName, 
            TeamName = teamName,
            UserSecret = userSecret,
        };

        var result = await _container.CreateItemAsync(participant, new PartitionKey(teamName));
        return result.Resource.UserSecret;
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
