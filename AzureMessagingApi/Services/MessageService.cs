using AzureMessagingApi.Models;
using AzureMessagingApi.Repositories;
using AzureMessagingApi.Telemetry;

namespace AzureMessagingApi.Services;

public class MessageService
{
    private readonly IMessageRepository _repository;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<MessageService> _logger;

    public MessageService(IMessageRepository repository, IMessagePublisher publisher, ILogger<MessageService> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Message> SendMessageAsync(SendMessageRequest request)
    {
        using var activity = Instrumentation.ActivitySource.StartActivity("Messages.Send");
        activity?.SetTag("messaging.sender_id", request.SenderId);
        activity?.SetTag("messaging.recipient_id", request.RecipientId);

        var message = new Message
        {
            SenderId = request.SenderId,
            RecipientId = request.RecipientId,
            Content = request.Content
        };

        var created = await _repository.CreateAsync(message);
        activity?.SetTag("messaging.message_id", created.Id);

        await _publisher.PublishAsync(created);

        _logger.LogInformation(
            "Sent message {MessageId} from {SenderId} to {RecipientId}",
            created.Id, created.SenderId, created.RecipientId);

        return created;
    }

    public Task<IEnumerable<Message>> GetMessagesAsync(string userId)
        => _repository.GetByRecipientAsync(userId);

    public Task<IEnumerable<Message>> GetUnreadMessagesAsync(string userId)
        => _repository.GetUnreadByRecipientAsync(userId);

    public async Task<Message?> MarkAsReadAsync(string id, string recipientId)
    {
        var message = await _repository.GetByIdAsync(id, recipientId);
        if (message == null) return null;

        message.Status = "read";
        return await _repository.UpdateAsync(message);
    }
}
