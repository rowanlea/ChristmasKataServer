using Microsoft.Azure.Cosmos;

namespace WebAPI.Database;

public class CosmosDbClient : ICosmosDbClient
{
    readonly Container _container;
    public CosmosDbClient(IConfiguration configuration)
    {
        CosmosClient cosmosClient = new CosmosClient(
            configuration["accountEndpoint"], configuration["accountKey"]);

        var db = cosmosClient.GetDatabase("ChristmasKataServer");
        
        _container = db.GetContainer("participants");
    }

    public Container Container => _container;
}