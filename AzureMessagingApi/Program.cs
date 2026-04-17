using Microsoft.Azure.Cosmos;
using Azure.Messaging.ServiceBus;
using AzureMessagingApi.Repositories;
using AzureMessagingApi.Services;
using AzureMessagingApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Cosmos DB client
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["CosmosDb:ConnectionString"];
    var options = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway,
        RequestTimeout = TimeSpan.FromSeconds(30)
    };

    return new CosmosClient(connectionString, options);
});

// Register Service Bus client
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ServiceBusClient(config["ServiceBus:ConnectionString"]);
});

// Register services
builder.Services.AddScoped<IMessageRepository, CosmosMessageRepository>();
builder.Services.AddScoped<MessageService>();

// Cosmos health state (singleton, shared between initializer and health endpoint)
builder.Services.AddSingleton<CosmosHealthState>();

// Register background workers — CosmosInitializer first so it starts before DeliveryWorker
builder.Services.AddHostedService<CosmosInitializer>();
builder.Services.AddHostedService<DeliveryWorker>();

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();

// Health endpoints — API liveness + Cosmos readiness (init runs in background)
app.MapGet("/health", () => Results.Ok(new { status = "alive", timestamp = DateTime.UtcNow }));
app.MapGet("/health/cosmos", (CosmosHealthState state) =>
    Results.Ok(new
    {
        ready = state.IsReady,
        readyAt = state.ReadyAt,
        lastError = state.LastError
    }));

app.Run();
