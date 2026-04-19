using Azure.Messaging.ServiceBus;
using AzureMessagingApi.Models;
using AzureMessagingApi.Repositories;
using AzureMessagingApi.Telemetry;
using System.Text.Json;

namespace AzureMessagingApi.Services;

public class DeliveryWorker : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryWorker> _logger;

    public DeliveryWorker(ServiceBusClient serviceBusClient, IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<DeliveryWorker> logger)
    {
        var queueName = configuration["ServiceBus:QueueName"];
        _processor = serviceBusClient.CreateProcessor(queueName);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        using var activity = Instrumentation.ActivitySource.StartActivity("Messages.Deliver");

        var message = JsonSerializer.Deserialize<Message>(args.Message.Body.ToString());
        if (message == null)
        {
            _logger.LogWarning("Received Service Bus message with unparseable body; completing to skip");
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        activity?.SetTag("messaging.message_id", message.Id);
        activity?.SetTag("messaging.recipient_id", message.RecipientId);

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var stored = await repository.GetByIdAsync(message.Id, message.RecipientId);
        if (stored != null)
        {
            stored.Status = "delivered";
            await repository.UpdateAsync(stored);
            _logger.LogInformation("Message {MessageId} delivered to {RecipientId}", message.Id, message.RecipientId);
        }
        else
        {
            _logger.LogWarning("Message {MessageId} not found in Cosmos when delivering — possible race or orphan", message.Id);
        }

        await args.CompleteMessageAsync(args.Message);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus processing error: {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
