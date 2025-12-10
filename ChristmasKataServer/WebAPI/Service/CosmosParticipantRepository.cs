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

    public IEnumerable<Participant> GetAllParticipants()
    {
        throw new NotImplementedException();
    }

    public async Task<Participant?> GetParticipantBy(string teamName)
    {
        try
        {
            var userSecret = $"{teamName}-secret-${RandomNumberGenerator.GetInt32(1000, 9999)}";
            var p = new Participant
            {
                Id = teamName,
                TeamName = teamName,
                UserSecret = userSecret,
            };

            var result = await _container.UpsertItemAsync<Participant>(p);

        }
        catch (Exception ex)
        {
            var e = ex; 
        }

        // var participant = await _container.ReadItemAsync<Participant>(id: teamName, new PartitionKey(teamName));
        // return  participant?.Resource ?? null; 

        return null;
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

    public bool ParticipantExists(string teamName, string teamSecret)
    {
        var participant = _container.GetItemLinqQueryable<Participant>()
            .FirstOrDefault(x => x.TeamName == teamName && x.UserSecret == teamSecret);

        return participant != null;
    }

    public async Task UpdateParticipantScore(Participant participant)
    {
        await _container.ReplaceItemAsync(participant, participant.TeamName, new PartitionKey(participant.TeamName));
    }
}