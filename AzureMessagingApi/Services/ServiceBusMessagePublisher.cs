using Azure.Messaging.ServiceBus;
using AzureMessagingApi.Models;
using System.Text.Json;

namespace AzureMessagingApi.Services;

public class ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public ServiceBusMessagePublisher(ServiceBusClient client, IConfiguration configuration)
    {
        var queueName = configuration["ServiceBus:QueueName"]
            ?? throw new InvalidOperationException("ServiceBus:QueueName is not configured");
        _sender = client.CreateSender(queueName);
    }

    public async Task PublishAsync(Message message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(message);
        await _sender.SendMessageAsync(new ServiceBusMessage(payload), cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
