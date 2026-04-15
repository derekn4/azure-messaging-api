using Microsoft.Azure.Cosmos;
using AzureMessagingApi.Models;

namespace AzureMessagingApi.Repositories;

public class CosmosMessageRepository : IMessageRepository
{
    private readonly Container _container;

    public CosmosMessageRepository(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = configuration["CosmosDb:DatabaseName"];
        var containerName = configuration["CosmosDb:ContainerName"];
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task<Message> CreateAsync(Message message)
    {
        var response = await _container.CreateItemAsync(message, new PartitionKey(message.RecipientId));
        return response.Resource;
    }

    public async Task<Message?> GetByIdAsync(string id, string recipientId)
    {
        try
        {
            var response = await _container.ReadItemAsync<Message>(id, new PartitionKey(recipientId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Message>> GetByRecipientAsync(string recipientId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.RecipientId = @recipientId ORDER BY c.Timestamp DESC")
            .WithParameter("@recipientId", recipientId);

        return await ExecuteQueryAsync(query);
    }

    public async Task<IEnumerable<Message>> GetUnreadByRecipientAsync(string recipientId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.RecipientId = @recipientId AND c.Status != 'read' ORDER BY c.Timestamp DESC")
            .WithParameter("@recipientId", recipientId);

        return await ExecuteQueryAsync(query);
    }

    public async Task<Message> UpdateAsync(Message message)
    {
        var response = await _container.ReplaceItemAsync(message, message.Id, new PartitionKey(message.RecipientId));
        return response.Resource;
    }

    private async Task<IEnumerable<Message>> ExecuteQueryAsync(QueryDefinition queryDefinition)
    {
        var results = new List<Message>();
        using var iterator = _container.GetItemQueryIterator<Message>(queryDefinition);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }
}
