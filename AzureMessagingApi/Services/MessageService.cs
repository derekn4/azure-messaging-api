using Azure.Messaging.ServiceBus;
using AzureMessagingApi.Models;
using AzureMessagingApi.Repositories;
using System.Text.Json;

namespace AzureMessagingApi.Services;

public class MessageService
{
    private readonly IMessageRepository _repository;
    private readonly ServiceBusSender _sender;

    public MessageService(IMessageRepository repository, ServiceBusClient serviceBusClient, IConfiguration configuration)
    {
        _repository = repository;
        var queueName = configuration["ServiceBus:QueueName"];
        _sender = serviceBusClient.CreateSender(queueName);
    }

    public async Task<Message> SendMessageAsync(SendMessageRequest request)
    {
        var message = new Message
        {
            SenderId = request.SenderId,
            RecipientId = request.RecipientId,
            Content = request.Content
        };

        var created = await _repository.CreateAsync(message);

        var busMessage = new ServiceBusMessage(JsonSerializer.Serialize(created));
        await _sender.SendMessageAsync(busMessage);

        return created;
    }

    public async Task<IEnumerable<Message>> GetMessagesAsync(string userId)
    {
        return await _repository.GetByRecipientAsync(userId);
    }

    public async Task<IEnumerable<Message>> GetUnreadMessagesAsync(string userId)
    {
        return await _repository.GetUnreadByRecipientAsync(userId);
    }

    public async Task<Message?> MarkAsReadAsync(string id, string recipientId)
    {
        var message = await _repository.GetByIdAsync(id, recipientId);
        if (message == null) return null;

        message.Status = "read";
        return await _repository.UpdateAsync(message);
    }
}
