# Architecture and Design Decisions

## Asynchronous Processing Model

### Decision
All order processing is performed asynchronously.

### Rationale
- Prevents frontend blocking
- Absorbs traffic spikes
- Decouples ingestion from processing
- Enables independent scaling of system components

This aligns with event-driven architecture patterns used in high-throughput systems.

---

## CorrelationId vs OrderId

### Decision
Use two distinct identifiers:
- CorrelationId for technical workflow tracking
- OrderId as the database-generated business identifier

### Rationale
- Orders must be accepted before database interaction
- Asynchronous workflows require correlation across queues, Redis, and WebSockets
- Business identity remains a persistence concern
- Improves observability and traceability across distributed components

---

## API Gateway Selection (Azure API Management)

### Decision
Use Azure API Management as an **edge API gateway** in front of the Kubernetes cluster.

### Rationale
APIM is used as a **perimeter and governance layer**, not as a business gateway.

Its responsibilities are limited to:
- JWT authentication at the edge
- Rate limiting and quotas
- Traffic shaping and backend protection
- Acting as a stable public entry point

All business authorization and routing logic remains inside the Kubernetes cluster.

---

## Application Runtime

### Decision
Deploy all backend services on Azure Kubernetes Service.

### Rationale
- Enables stateless service design
- Supports horizontal scaling
- Provides workload isolation
- Aligns with modern cloud-native practices

Only a single cluster is shown for conceptual clarity.  
The architecture supports zonal and multi-cluster deployments without requiring logical changes.

---

## Order Workflow State Management

### Decision
Store short-lived order workflow state in Redis (ACCEPTED / PROCESSING / COMPLETED).

### Rationale
- WebSocket connections are transient by nature.
- Users may open multiple browser tabs or reconnect at any time.
- Order state must be consistently observable across all application instances.
- Redis provides low-latency, shared state without introducing coupling between services.

Redis is used only for **transient workflow state**, not as a system of record.
The authoritative business state remains in the SQL database.

---

## State Management

### Decision
All services are stateless.

### Rationale
- Simplifies scaling and deployments
- Improves fault tolerance
- Reduces coupling between services

State is externalized to:
- Azure SQL Database (business state)
- Redis (session and correlation state)

---

## Redis Usage

### Decision
Use Redis as a short-lived correlation and session registry.

### Rationale
- Enables real-time notification in asynchronous workflows
- Avoids sticky sessions for WebSocket connections
- Supports horizontal scaling of notification services
- TTL-based cleanup prevents stale state accumulation

Redis is not used as a message broker or system of record.


---

## Real-time Notifications (SignalR) and Scale-Out

### Decision
Use **ASP.NET Core SignalR** for real-time order updates, and enable **scale-out** with a **Redis backplane** (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) using the project’s existing Redis instance. Backplane pub/sub traffic is isolated with a Redis **`ChannelPrefix`** (e.g., `contoso-signalr`).

### Rationale
- **Multi-pod fan-out:** when a worker calls `Clients.User(userId)`, the sending pod publishes the message to Redis pub/sub; *all* SignalR pods receive it and forward it to their **local WebSocket connections** for that `userId`. This ensures a user with multiple tabs/devices connected to different pods still receives every notification.
- **Redis reuse without collisions:** the same Redis instance can store `order:*` workflow keys and also carry backplane pub/sub. Using a **`ChannelPrefix`** prevents backplane channels from colliding with other Redis pub/sub consumers and keeps the backplane logically isolated.
- **No PII in messages:** the Hub targets users via `Context.UserIdentifier` derived from the JWT (`sub`), while the workflow uses Redis `order:map:{correlationId} -> userId` mapping. Service Bus events do not contain `userId`.

---

## Sticky Sessions (Ingress-pod Affinity)

### Decision
Do **not** rely on sticky sessions / ingress affinity for SignalR.

### Rationale
- **Portability and simplicity:** sticky sessions couple correctness to a specific ingress/controller configuration and increase operational complexity.
- **Load distribution:** WebSockets are long-lived by nature; affinity can lead to uneven pod utilization when many clients stay pinned to the same endpoints.
- **Deterministic connection establishment:** the client forces **WebSockets-only** with **`skipNegotiation: true`** so the SignalR connection is established in a single WebSocket hop. This avoids the classic cross-pod `/negotiate` vs WebSocket upgrade mismatch.
- **Resilience during scale events:** when pods scale in/out, existing sockets may drop, but clients automatically reconnect to any healthy pod and rehydrate state (e.g., by re-querying current order status backed by Redis TTL state).


---

## Messaging Strategy

### Decision
Use Azure Service Bus queues with FIFO semantics.

### Rationale
- Reliable delivery with retry support
- Built-in dead-letter queues for poison messages
- Well suited for transactional, ordered workflows

Topics and fan-out are intentionally excluded to keep the workflow simple and predictable.

---

## Failure Handling

### Decision
Adopt at-least-once delivery with defensive consumers.

### Rationale
- Consumers are idempotent
- Transient failures are retried automatically
- Poison messages are isolated via dead-letter queues

Dead-letter queues are treated as an operational concern and monitored separately.

---

## Integration with External Identity Providers

### Decision
Decouple identity management from backend services and integrate authentication at the API gateway layer.

### Rationale
Azure API Management is **identity-provider agnostic** and supports validation of JWTs issued by any OAuth 2.0 / OpenID Connect–compliant provider.

This allows the system to integrate with:
- Cloud-native IdPs
- Corporate identity platforms
- Hybrid or multi-cloud identity solutions

### Example Identity Providers
Typical examples of compatible IdPs include:
- Azure Active Directory / Entra ID
- Keycloak
- Auth0
- Okta

In all cases:
- The IdP issues JWT access tokens
- APIM validates token authenticity, issuer, and audience
- User identity claims are forwarded to backend services

The Identity Provider itself remains outside the scope of this architecture.

---

## Design Philosophy

This architecture prioritizes:
- Clear responsibility boundaries
- Predictable behavior under load
- Operational safety over theoretical optimization

Capacity planning and resource sizing are intentionally deferred to operational metrics rather than upfront assumptions.