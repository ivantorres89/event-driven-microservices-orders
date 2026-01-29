# Local Infrastructure (Docker Compose)

This folder contains the **local development infrastructure** required to run the full demo stack end-to-end using **Docker Compose**.

The local stack is designed to be:
- **Fast to start** (single command)
- **Reproducible** across machines
- **Aligned with the production architecture** (Azure-ready)
- **Easy to demo in interviews** (Swagger, tracing UI, broker UI)

> In production, the platform runs on Azure-managed services (AKS, Azure Service Bus, Azure SQL Database, Azure Monitor / Application Insights).
> Locally we use lightweight containers to simulate the same architectural behaviors.

---

## What Gets Deployed Locally

### Backend Services
- **order-accept** (HTTP API + publishes `OrderAccepted`)
- **order-process** (background worker + persists state + publishes `OrderProcessed`)
- **order-notification** (WebSockets/SignalR + pushes real-time updates)

### Frontend
- **SPA** (minimal UI to submit orders and display notifications)

### Shared Infrastructure
- **RabbitMQ** (local broker used for development & integration testing)
  - In production: **Azure Service Bus**
- **Redis** (ephemeral workflow/session state)
- **MS SQL Server** (local relational database)
  - Chosen for similarity to **Azure SQL Database** and to keep SQL Server semantics consistent
- **OpenTelemetry Collector** (OTLP “facade” endpoint for telemetry)
- **Jaeger UI** (distributed tracing UI for local demos)

---

## Production Observability Notes

This local environment is intentionally lightweight.

In production, observability is typically handled as follows:
- **Metrics/monitoring**: Azure Monitor
- **Traces**: Azure Application Insights (via OpenTelemetry exporter or native integration)
- **Logs**: centralized logging (e.g., **ELK** or **Grafana Loki**), plus Azure-native log storage depending on org standards

Locally we prioritize **distributed tracing** to demonstrate correlation and workflows across services.

---

## Prerequisites (Windows 11)

- **Docker Desktop** (Community is fine)
- Docker Compose v2 (`docker compose ...`)
- Recommended: WSL2 backend enabled in Docker Desktop
- Ensure these ports are available:
  - `8081-8084` (services + SPA)
  - `1433` (SQL Server)
  - `6379` (Redis)
  - `5672` (RabbitMQ)
  - `15672` (RabbitMQ UI)
  - `4317` / `4318` (OTLP gRPC / HTTP)
  - `16686` (Jaeger UI)

Verify:
docker --version
docker compose version

## Prerequisites (Windows 11)

**docker compose up -d --build**

That command builds the services (if needed) and starts all infrastructure containers.

To stop everything:

**docker compose down**

To stop and remove volumes (full reset):

**docker compose down -v**

## Useful URLs
### Services

- order-accept Swagger: http://localhost:8081/swagger
- order-process: (worker, no HTTP endpoint by default)
- order-notification: http://localhost:8083 (WebSocket/SignalR service endpoint)
- SPA: http://localhost:8080

### Infrastructure

- RabbitMQ Management UI: http://localhost:15672

Username: guest
Password: guest

- Jaeger UI (Traces): http://localhost:16686

## Configuration (Local)

Local configuration is provided via:

- docker-compose.yml (root)
- environment variables injected into containers
- service appsettings.json defaults (as fallback)

### Messaging

Local broker: RabbitMQ
Queue names are configurable per service via environment variables / appsettings.
In production, the same abstractions are backed by Azure Service Bus.

### Redis

Redis is used only for ephemeral state:

WebSocket session registry (TTL-based)

Workflow status cache (ACCEPTED / PROCESSING / COMPLETED) keyed by CorrelationId

Redis is not a system of record.

### SQL Server

MS SQL Server is used locally to keep the relational experience close to Azure SQL Database:

similar T-SQL behaviors

familiar constraints/transactions

predictable driver compatibility

Note: you may need to adjust SA_PASSWORD in the compose file depending on local security policies.

Use SQL Server Management Studio to connect to the SQL Server database with these params:
- server name: localhost, 1433
- login: sa
- password: Your_strong_Password123!
- Encryption: Optional or trust the certificate.

### Demo Walkthrough (Suggested)

1. Open the SPA: http://localhost:8080
2. Submit a new order

3. Observe:

- order-accept returns immediately (HTTP 202/Accepted)
- Messages appear in RabbitMQ (optional check)
- Workflow state stored in Redis (optional check)
- Trace visible in Jaeger (CorrelationId propagated)

This demonstrates:

- event-driven ingestion
- async processing pipeline
- correlation across services
- real-time notification flow

