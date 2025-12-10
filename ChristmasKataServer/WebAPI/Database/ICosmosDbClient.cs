using Microsoft.Azure.Cosmos;

namespace WebAPI.Database;

public interface ICosmosDbClient
{
    public Container Container { get;  }
}