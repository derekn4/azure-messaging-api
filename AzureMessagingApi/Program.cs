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
    return new CosmosClient(config["CosmosDb:ConnectionString"]);
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

// Register background worker
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

app.Run();
