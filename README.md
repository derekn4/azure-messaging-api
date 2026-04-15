# Azure Messaging API

Real-time messaging API built with ASP.NET Core 8, Azure Cosmos DB, and Azure Service Bus. Deployed on Azure Kubernetes Service.

## Tech Stack

- ASP.NET Core 8 Web API
- Azure Cosmos DB (NoSQL document store)
- Azure Service Bus (async message queue)
- Azure Kubernetes Service (container orchestration)
- xUnit (testing)

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/messages | Send a message |
| GET | /api/messages/{userId} | Get all messages for a user |
| GET | /api/messages/{userId}/unread | Get unread messages |
| PATCH | /api/messages/{id}/read | Mark message as read |
| GET | /api/health | Health check |

## Running Locally

```bash
cd AzureMessagingApi
dotnet run
```

## Running Tests

```bash
dotnet test
```
