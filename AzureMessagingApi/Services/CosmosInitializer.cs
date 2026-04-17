using Microsoft.Azure.Cosmos;

namespace AzureMessagingApi.Services;

public class CosmosInitializer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CosmosInitializer> _logger;
    private readonly CosmosHealthState _healthState;

    public CosmosInitializer(
        IServiceProvider serviceProvider,
        ILogger<CosmosInitializer> logger,
        CosmosHealthState healthState)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthState = healthState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CosmosInitializer: starting background init");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var dbName = config["CosmosDb:DatabaseName"];
            var containerName = config["CosmosDb:ContainerName"];

            _logger.LogInformation("CosmosInitializer: creating database '{DbName}'", dbName);
            var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                dbName,
                cancellationToken: stoppingToken);

            _logger.LogInformation("CosmosInitializer: creating container '{ContainerName}'", containerName);
            await database.Database.CreateContainerIfNotExistsAsync(
                containerName,
                "/recipientId",
                cancellationToken: stoppingToken);

            _healthState.IsReady = true;
            _healthState.ReadyAt = DateTime.UtcNow;
            _logger.LogInformation("CosmosInitializer: ready");
        }
        catch (Exception ex)
        {
            _healthState.LastError = ex.Message;
            _logger.LogError(ex, "CosmosInitializer: failed");
        }
    }
}
