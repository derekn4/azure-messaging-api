using AzureMessagingApi.Models;

namespace AzureMessagingApi.Services;

public interface IMessagePublisher
{
    Task PublishAsync(Message message, CancellationToken cancellationToken = default);
}
