# Azure Messaging API

Distributed real-time messaging API built with ASP.NET Core 8, Azure Cosmos DB, and Azure Service Bus. HTTP writes are durable, delivery is asynchronous via a Service Bus queue processed by a background worker.

## Tech Stack

- ASP.NET Core 8 Web API
- Azure Cosmos DB (NoSQL document store, partition key = `/recipientId`)
- Azure Service Bus (async message queue)
- Docker Compose (local Cosmos + Service Bus emulators)
- xUnit (testing — planned)
- Azure Kubernetes Service (deployment — planned)

## Architecture

```
  Client
    |
    v
 [Controller] --> [MessageService] --> [Cosmos: status=pending]
                        |
                        v
                [Service Bus queue]
                        |
                        v
                [DeliveryWorker (BackgroundService)]
                        |
                        v
                 [Cosmos: status=delivered]
```

Full walkthrough of the code flow, design decisions, and debugging lessons: [`../gap_projects_plans/azure-messaging-api-code-flow.md`](../gap_projects_plans/azure-messaging-api-code-flow.md)

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/messages | Send a message (writes Cosmos + enqueues to Service Bus) |
| GET | /api/messages/{userId} | List all messages for a recipient (newest first) |
| GET | /api/messages/{userId}/unread | List unread messages for a recipient |
| PATCH | /api/messages/{id}/read?recipientId=X | Mark a message as read |
| GET | /health | Liveness probe |
| GET | /health/cosmos | Cosmos readiness (`ready`, `readyAt`, `lastError`) |

## Running Locally

### Prerequisites
- .NET 8 SDK
- Docker Desktop
- Windows Trusted Root store should contain the Cosmos emulator's self-signed cert (see below)

### 1. Start the emulators

```bash
docker compose up -d
```

This brings up three containers: `cosmosdb-emulator` (port 8081), `sqledge` (Cosmos's backend store), and `servicebus-emulator` (port 5672).

### 2. Trust the Cosmos emulator cert (one-time)

```bash
# Download the cert from the running emulator
curl.exe -k https://localhost:8081/_explorer/emulator.pem -o cosmos-emulator.crt

# Import into Windows Trusted Root (run in Admin PowerShell)
Import-Certificate -FilePath cosmos-emulator.crt -CertStoreLocation Cert:\LocalMachine\Root
```

### 3. Run the API

```bash
cd AzureMessagingApi
dotnet run
```

Watch for:
```
info: AzureMessagingApi.Services.CosmosInitializer[0]
      CosmosInitializer: ready
```
Kestrel comes up on `http://localhost:5253`.

### 4. Send a test message

```bash
curl.exe -X POST http://localhost:5253/api/messages \
  -H "Content-Type: application/json" \
  -d '{"senderId":"alice","recipientId":"bob","content":"hello"}'
```

Then read it back:
```bash
curl.exe http://localhost:5253/api/messages/bob
```
You should see `"status":"delivered"` — the DeliveryWorker has already picked it up from the queue.

## Configuration

`appsettings.json` holds placeholder connection strings for production. `appsettings.Development.json` has the local emulator values and is loaded automatically when `ASPNETCORE_ENVIRONMENT=Development`.

| Key | Purpose |
|---|---|
| `CosmosDb:ConnectionString` | Cosmos endpoint + key |
| `CosmosDb:DatabaseName` | Auto-created on startup by `CosmosInitializer` |
| `CosmosDb:ContainerName` | Auto-created with partition key `/recipientId` |
| `ServiceBus:ConnectionString` | Service Bus endpoint + SAS key |
| `ServiceBus:QueueName` | Queue that the worker consumes |

## Key design decisions

- **Gateway mode with `LimitToEndpoint = true`** — Direct mode needs TCP ports Docker doesn't expose. Without `LimitToEndpoint`, the SDK tries to reach partition addresses at Docker-internal IPs and hangs silently.
- **Cosmos init as a `BackgroundService`** — initialization runs in parallel with Kestrel startup. If it fails, the API still starts and reports the failure via `/health/cosmos` instead of hanging.
- **Scoped services + `IServiceScopeFactory` inside the worker** — `BackgroundService` is a singleton; it can't inject scoped repositories directly. The worker creates a scope per message.
- **`CosmosSerializationOptions.PropertyNamingPolicy = CamelCase`** — the SDK uses Newtonsoft.Json, which ignores System.Text.Json `[JsonPropertyName]` attributes. Setting this one option makes `Id` serialize as `id` (required by Cosmos) and all queries use consistent camelCase field names.

## Running tests

```bash
dotnet test
```

(Test project is planned — not yet implemented.)

## Deployment (planned)

- Dockerize the API (`Dockerfile` in `AzureMessagingApi/`)
- Push to Azure Container Registry
- Deploy to AKS via a Kubernetes manifest (`k8s/deployment.yaml` + `k8s/service.yaml`)
- Use Azure Key Vault for connection strings in production
