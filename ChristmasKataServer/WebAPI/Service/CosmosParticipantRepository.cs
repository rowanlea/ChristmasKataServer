using System.Security.Cryptography;
using Microsoft.Azure.Cosmos;
using WebAPI.Database;

namespace WebAPI.Service;

public class CosmosParticipantRepository : IParticipantRepository
{
    private readonly Container _container;
    
    public CosmosParticipantRepository(ICosmosDbClient client)
    {
        _container = client.Container;
    }
    public Participant TryGetOrCreateParticipant(string teamName, string secret)
    {
        throw new NotImplementedException();
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

    public async Task UpdateParticipantScore(Participant participant)
    {
        await _container.ReplaceItemAsync(participant, participant.TeamName, new PartitionKey(participant.TeamName));
    }
}