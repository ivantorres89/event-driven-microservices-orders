# Contoso Shop — Implemented Architecture (event-driven-microservices-orders)

This document describes the **implemented** architecture in this repository: components, responsibilities, message contracts, Redis usage, SignalR scale-out, and the operational workflow from “checkout” to real-time notification.

> If you are looking for “how to run it locally”, start with the repo root `README.md` and `infra/local/README.md`.

---

## Architecture at a glance

![Architectural design](/eventdriven-k8s-websockets-redisbackplane-orderprocessing.jpg)

The system implements an **event-driven, asynchronous order workflow** with **real-time UX**:

- The SPA submits an order over **HTTP**.
- The request is **accepted quickly**, a `CorrelationId` is created, and the order is **enqueued**.
- A background worker persists the order in **SQL** and publishes a completion event.
- A notification service pushes a **SignalR** message to the user over **WSS**, even across multiple pods (Redis backplane).

---

## Core invariants

1) **Fast intake, async processing**  
   The intake API responds immediately and does not block on downstream processing.

2) **At-least-once messaging**  
   Messages can be delivered more than once. Consumers must be **idempotent**.

3) **SQL is the System of Record**  
   SQL (Azure SQL / SQL Server) holds authoritative business state. Redis is **ephemeral** (TTL).

4) **No sticky sessions for WebSockets**  
   SignalR is scaled out across pods using a **Redis backplane** so `Clients.User(userId)` works across replicas.

5) **CorrelationId is the workflow key**  
   `CorrelationId` ties together: HTTP request, broker messages, Redis workflow keys, SignalR notifications, and tracing/logging.

---

## Runtime targets

### Local development (Docker Compose)
The repo runs end-to-end locally with:

- RabbitMQ (broker, local dev replacement for Service Bus)
- Redis (workflow state + SignalR backplane)
- SQL Server (OLTP)
- OpenTelemetry Collector + Jaeger (traces)
- The three backend services + the demo SPA

Key URLs (defaults):
- SPA: `http://localhost:4200`
- order-notification: `https://localhost:5007` (Hub: `/hubs/order-status`, DEV token: `POST /dev/token`)
- RabbitMQ UI: `http://localhost:15672`
- Jaeger UI: `http://localhost:16686`

### Azure / production mapping (conceptual)
In Azure the same logical architecture maps to:

- **API Management** as edge gateway (JWT validation, throttling, public perimeter)
- **AKS** for microservices + autoscaling
- **Azure Service Bus** queues for messaging
- **Azure Cache for Redis** for workflow state + SignalR backplane
- **Azure SQL Database** for OLTP persistence

The repository keeps the code broker-agnostic via messaging abstractions, so the local broker (RabbitMQ) can be swapped for Azure Service Bus.

---

## Components and responsibilities

### 1) SPA (Angular)
- Establishes a **SignalR** connection to receive notifications.
- Submits orders to the intake API.
- Uses `CorrelationId` to bind UI state to the asynchronous backend workflow.

### 2) `order-accept` (HTTP ingress / intake boundary)
Primary role: **fast acceptance** of client requests.

Responsibilities:
- Authenticate the request (JWT) and derive the **user identity** from token claims.
- Generate a **CorrelationId**.
- Initialize transient workflow state in Redis:
  - `order:status:{correlationId} = ACCEPTED`
  - `order:map:{correlationId} = {userId}`
- Publish `OrderAccepted` to the inbound queue (`order.accepted`).
- Return `201 Created` to the client (with `CorrelationId` for tracking).

Non-responsibilities:
- Does **not** perform asynchronous processing.
- Does **not** treat Redis as durable state.
- The authoritative order record is written by the processing worker (SQL is the SoR).

> Note: the intake layer may still perform **lightweight** data lookups (e.g., product validation / customer normalization),
> but it does not execute the “long-running” order processing transaction.

### 3) `order-process` (async worker / OLTP boundary)
Primary role: **execute the business transaction**.

Responsibilities:
- Consume `OrderAccepted` (FIFO) from `order.accepted`.
- Update Redis transient status:
  - `PROCESSING` → `COMPLETED`
  - Refresh TTL on `order:map:{correlationId}` while processing.
- Persist `Order` + `OrderItems` into SQL inside a single OLTP transaction (SQL generates `OrderId`).
- Publish `OrderProcessed(CorrelationId, OrderId)` to `order.processed`.

### 4) `order-notification` (SignalR + consumer)
Primary role: **push real-time notifications**.

Responsibilities:
- Host the SignalR hub `/hubs/order-status` over HTTPS/WSS.
- Use Redis backplane for SignalR scale-out (no sticky sessions required).
- Consume `OrderProcessed` from `order.processed`.
- Resolve `userId` from Redis using `CorrelationId`:
  - `GET order:map:{correlationId}`
- Notify all active connections for that user:
  - `Clients.User(userId).Notification({ correlationId, status, orderId })`

If `order:map:{correlationId}` is missing, the consumer:
- Performs **short local retries** (100ms → 250ms → 500ms),
- Then throws to trigger broker retry / DLQ rather than silently losing notifications.

---

## Messaging model

### Queues
- **Inbound** queue (to processing): `order.accepted`
- **Outbound** queue (to notification): `order.processed`

Both are treated as FIFO queues (predictable ordering) with **at-least-once** delivery.

### Integration events (contracts)
- `OrderAcceptedEvent(CorrelationId, CreateOrderRequest)`
  - Contains the order payload required for processing (items, customer reference).
  - Does not carry SignalR routing metadata.
- `OrderProcessedEvent(CorrelationId, OrderId)`
  - Sent after persistence succeeds (or idempotent “already persisted”).

### Broker swapping (RabbitMQ ↔ Service Bus)
- Local: RabbitMQ (because Service Bus has no official local emulator)
- Cloud: Azure Service Bus

The services depend on **messaging abstractions**, so business logic remains broker-agnostic.

---

## Redis model

Redis is used for two distinct purposes:

### 1) Workflow status (ephemeral, TTL)
Key:
- `order:status:{correlationId}`

Values:
- `ACCEPTED`
- `PROCESSING`
- `COMPLETED|{orderId}` (the worker stores `OrderId` once known)

### 2) Correlation map (ephemeral, TTL)
Key:
- `order:map:{correlationId}` → `{userId}`

Purpose:
- Route notifications without embedding “who to notify” into broker messages.

### TTL and refresh
- Typical TTL: **30–60 minutes** (config-driven).
- `order-process` refreshes TTL on the correlation map while processing to avoid expiration during long runs.

Redis is not a system of record. If a key expires, the system remains correct (SQL is authoritative) but the UX may lose “instant notification” for that correlation.

---

## SignalR scale-out (no sticky sessions)

This system intentionally does **not** rely on sticky sessions for WebSockets.

Instead:
- Each `order-notification` replica hosts a SignalR hub instance.
- The SPA’s WebSocket connection lands on *one* replica.
- SignalR uses **Redis pub/sub backplane** so that `Clients.User(userId)` broadcasts to all pods.
- Each pod delivers the notification only to its *local* WebSocket connections for that `userId`.

Backplane isolation:
- A Redis `ChannelPrefix` (default: `contoso-signalr`) isolates SignalR pub/sub channels from the `order:*` keys.

---

## End-to-end workflow (step-by-step)

1) **SPA connects to SignalR**  
   The SPA opens `wss://.../hubs/order-status` with a JWT. The hub maps `Context.UserIdentifier` from token claims.

2) **SPA submits an order** (`POST /api/orders`)  
   `order-accept`:
   - extracts `userId` from JWT claims,
   - generates `CorrelationId`,
   - writes Redis:
     - `SET order:map:{cid} {userId} EX <ttl>`
     - `SET order:status:{cid} ACCEPTED EX <ttl>`
   - publishes `OrderAccepted(correlationId, orderPayload)` to `order.accepted`,
   - returns `201 Created` with the `CorrelationId`.

3) **Worker processes** (`order-process`)  
   - consumes `OrderAccepted`,
   - sets `order:status:{cid} = PROCESSING` and refreshes TTL for `order:map:{cid}`,
   - persists the order in SQL (generates `OrderId`),
   - sets `order:status:{cid} = COMPLETED|{orderId}`,
   - publishes `OrderProcessed(correlationId, orderId)` to `order.processed`.

4) **Notification is pushed** (`order-notification`)  
   - consumes `OrderProcessed`,
   - resolves `userId` via `GET order:map:{cid}`,
   - sends `Clients.User(userId).Notification({ correlationId, status, orderId })`,
   - the Redis backplane fans out across pods, so all tabs/devices receive the message.

---

## Reliability and failure handling

### Delivery semantics
- The broker is **at-least-once**. Duplicate delivery is normal.

### Idempotency
- `order-process` uses `CorrelationId` as an idempotency key:
  - if `CorrelationId` was already persisted, it reuses the existing `OrderId`
  - it may still publish `OrderProcessed` so notifications are not missed

### Retries and poison handling
- Local RabbitMQ listeners implement retry-by-republish with an `x-retry-count` header.
- After max attempts, the message is rejected (expected to land in DLQ via DLX if configured).
- In Azure Service Bus, retries + DLQ are handled by the platform.

### Redis mapping missing
If `order-notification` cannot resolve the mapping, it throws after short local retries so the message is retried / DLQ’d rather than silently dropped.

---

## Observability

This repository implements “portfolio-grade” observability:

- **Structured logs** via Serilog (CorrelationId is included in log scope/templates)
- **Distributed tracing + metrics** via OpenTelemetry
- A local **OTEL collector** exports to **Jaeger**

Correlation propagation:
- Consumers extract `CorrelationId` from the **event payload** and store it in an ambient correlation context.
- Trace context is propagated via broker headers when available.
- Redis operations create explicit spans so workflow visibility remains strong even without auto-instrumentation.

---

## Security model

Production-like intent:
- JWT validation and public perimeter controls (throttling, quotas) live at the **gateway** layer (API Management).

Local/dev conveniences:
- `order-accept` validates JWTs using either:
  - an external authority (OIDC metadata), or
  - a symmetric signing key (dev/demo)
- `order-notification` provides a DEV-only `POST /dev/token` endpoint to mint test JWTs for SPA + SignalR.
- SignalR uses `?access_token=` because browsers cannot attach `Authorization` headers during WebSocket upgrade.

---

## Where to look in the repo

- `docker-compose.yml` — full local stack
- `infra/local/` — local HTTPS certificate scripts + OTEL collector config
- `services/order-accept/` — HTTP ingress + publish `OrderAccepted`
- `services/order-process/` — worker + SQL persistence + publish `OrderProcessed`
- `services/order-notification/` — SignalR hub + consumer + Redis backplane
- `design/` — diagram + scale-out notes

