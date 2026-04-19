# Azure Messaging API

Distributed real-time messaging API built with ASP.NET Core 8, Azure Cosmos DB, and Azure Service Bus. HTTP writes are durable, delivery is asynchronous via a Service Bus queue processed by a background worker. Runs locally on Docker Desktop Kubernetes.

## Tech Stack

- **ASP.NET Core 8** Web API
- **Azure Cosmos DB** (NoSQL document store, partition key = `/recipientId`)
- **Azure Service Bus** (async message queue)
- **Docker Compose** (local Cosmos + Service Bus emulators)
- **Kubernetes** (local via Docker Desktop; manifests are AKS-ready)
- **Serilog** (structured JSON logs, K8s log-aggregator friendly)
- **OpenTelemetry** (distributed tracing — ASP.NET Core, HttpClient, custom `ActivitySource`)
- **xUnit + Moq** (unit tests)

## Architecture

```
  Client
    |
    v
 [Controller] --> [MessageService] --> [IMessageRepository (Cosmos): status=pending]
                        |
                        v
                [IMessagePublisher (Service Bus queue)]
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

## Running Locally (plain .NET)

### Prerequisites
- .NET 8 SDK
- Docker Desktop
- Windows Trusted Root store should contain the Cosmos emulator's self-signed cert (see below)

### 1. Start the emulators

```bash
docker compose up -d
```

Brings up three containers: `cosmosdb-emulator` (port 8081), `sqledge` (Cosmos's backend store), and `servicebus-emulator` (port 5672).

### 2. Trust the Cosmos emulator cert (one-time, host runs only)

```bash
curl.exe -k https://localhost:8081/_explorer/emulator.pem -o cosmos-emulator.crt
# Admin PowerShell:
Import-Certificate -FilePath cosmos-emulator.crt -CertStoreLocation Cert:\LocalMachine\Root
```

Not needed for K8s runs — the ConfigMap sets `CosmosDb__TrustEmulatorCert=true`, which wires up a permissive cert validator inside the pod.

### 3. Run the API

```bash
cd AzureMessagingApi
dotnet run
```

Kestrel comes up on `http://localhost:5253`.

### 4. Send a test message

```bash
curl.exe -X POST http://localhost:5253/api/messages \
  -H "Content-Type: application/json" \
  -d '{"senderId":"alice","recipientId":"bob","content":"hello"}'
curl.exe http://localhost:5253/api/messages/bob
```

`status` should flip from `pending` to `delivered` within a second.

## Running on Kubernetes (Docker Desktop)

### Prerequisites
- Docker Desktop with Kubernetes enabled (Settings → Kubernetes → Enable Kubernetes, provisioner `kubeadm`)
- `kubectl` context set to `docker-desktop`

### 1. Start emulators + build the image

```bash
docker compose up -d
docker build -t azure-messaging-api:local .
```

> The compose file intentionally does **not** expose Cosmos direct-mode ports 10250-10255 — port 10250 collides with the Docker Desktop kubelet. The app uses `ConnectionMode.Gateway`, which only needs 8081.

### 2. Apply the manifests

```bash
kubectl apply -k k8s/
kubectl get pods -w
```

You get:

- 2 replicas behind a `LoadBalancer` service exposed on `localhost:80`
- Rolling update strategy (`maxSurge: 1, maxUnavailable: 0`) — zero-downtime deploys
- Liveness probe on `/health` — K8s restarts a pod whose process wedges
- Readiness probe on `/health/cosmos` — pods don't receive traffic until Cosmos init succeeds

### 3. Verify end-to-end

```bash
curl -X POST http://localhost/api/messages \
  -H "Content-Type: application/json" \
  -d '{"senderId":"alice","recipientId":"bob","content":"from k8s"}'
sleep 2
curl http://localhost/api/messages/bob
```

### 4. Iterate (rebuild + roll)

```bash
docker build -t azure-messaging-api:local .
kubectl rollout restart deployment/azure-messaging-api
kubectl rollout status deployment/azure-messaging-api
```

## Configuration

`appsettings.json` holds placeholder values. `appsettings.Development.json` has local emulator defaults and is loaded when `ASPNETCORE_ENVIRONMENT=Development`. In K8s, everything is sourced from `k8s/configmap.yaml`.

| Key | Purpose |
|---|---|
| `CosmosDb:ConnectionString` | Cosmos endpoint + key |
| `CosmosDb:DatabaseName` | Auto-created on startup by `CosmosInitializer` |
| `CosmosDb:ContainerName` | Auto-created with partition key `/recipientId` |
| `CosmosDb:TrustEmulatorCert` | Bypass cert validation for emulator (in-cluster pods only) |
| `ServiceBus:ConnectionString` | Service Bus endpoint + SAS key |
| `ServiceBus:QueueName` | Queue the worker consumes (`message-delivery`) |

## Key Design Decisions

- **Gateway mode with `LimitToEndpoint = true`** — Direct mode needs TCP ports Docker doesn't expose. Without `LimitToEndpoint`, the SDK tries to reach partition addresses at Docker-internal IPs and hangs silently.
- **Cosmos init as a `BackgroundService`** — initialization runs in parallel with Kestrel startup. If it fails, the API still starts and reports the failure via `/health/cosmos` instead of hanging.
- **`IMessagePublisher` interface in front of Service Bus** — lets `MessageService` be unit-tested without an emulator and keeps the service decoupled from the transport.
- **Scoped services + `IServiceScopeFactory` inside the worker** — `BackgroundService` is a singleton and can't inject scoped repositories directly. The worker creates a scope per message.
- **`CosmosSerializationOptions.PropertyNamingPolicy = CamelCase`** — the SDK uses Newtonsoft.Json, which ignores System.Text.Json `[JsonPropertyName]` attributes. One option makes `Id` serialize as `id` (required by Cosmos) and aligns all queries on camelCase.
- **Readiness probe on `/health/cosmos`** — returns 503 while `CosmosInitializer` is still running, so K8s holds traffic back from not-yet-ready pods.

## Observability

- **Serilog** replaces the default logger. Output is JSON via `CompactJsonFormatter` — one structured event per line, ready for Loki / Fluent Bit / Elasticsearch. Enriched with `MachineName` and `EnvironmentName`. Request logging is via `UseSerilogRequestLogging()` (one line per HTTP request with status + elapsed).
- **OpenTelemetry** tracing: auto-instruments ASP.NET Core (inbound) and HttpClient (outbound — covers the Cosmos SDK in Gateway mode). Custom spans for business operations (`Messages.Send`, `Messages.Deliver`) via a shared `ActivitySource` in `Telemetry/Instrumentation.cs`. Console exporter for local dev; swap for OTLP to hit Aspire Dashboard / Jaeger / Grafana Tempo / Honeycomb.

## Testing

```bash
dotnet test
```

Runs the xUnit test project in `AzureMessagingApi.Tests/`. Covers the `MessageService` send path (Cosmos-before-Service-Bus ordering, publish suppression on repository failure), read paths, and `MarkAsReadAsync` behavior (not-found, status update, partition-key threading). `IMessageRepository` and `IMessagePublisher` are mocked with Moq — no emulators required for unit tests.

## Deployment (planned)

- Push image to Azure Container Registry (`azmsgapi.azurecr.io`)
- Deploy the same `k8s/` manifests to AKS (`kubectl apply -k k8s/` — Kustomize overlay per environment would go on top)
- Move secrets out of `ConfigMap` into Azure Key Vault with the CSI driver
- Replace the Console exporter with OTLP → Azure Monitor / Grafana Tempo
