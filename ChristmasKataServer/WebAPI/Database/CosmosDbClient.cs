using Microsoft.Azure.Cosmos;

namespace WebAPI.Database;

public class CosmosDbClient : ICosmosDbClient
{
    readonly Container _container;
    public CosmosDbClient()
    {
        CosmosClient cosmosClient = new CosmosClient(
            "https://localhost:8081", 
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

        var db = cosmosClient.GetDatabase("ChristmasKataServer");
        
        _container = db.GetContainer("participants");
    }

    public Container Container => _container;
}