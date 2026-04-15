using Azure.Messaging.ServiceBus;
using AzureMessagingApi.Models;
using AzureMessagingApi.Repositories;
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
        var message = JsonSerializer.Deserialize<Message>(args.Message.Body.ToString());
        if (message == null) return;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var stored = await repository.GetByIdAsync(message.Id, message.RecipientId);
        if (stored != null)
        {
            stored.Status = "delivered";
            await repository.UpdateAsync(stored);
            _logger.LogInformation("Message {MessageId} delivered", message.Id);
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
