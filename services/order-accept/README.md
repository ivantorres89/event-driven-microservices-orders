# Order Accept Service

## Purpose

The **Order Accept Service** is the **entry point** of the order processing platform.

Its sole responsibility is to **accept incoming order requests quickly and safely**, generate a technical identifier for correlation, and **publish the request to the asynchronous processing pipeline**.

This service is intentionally **thin** and performs **no synchronous business processing**.

---

## Responsibilities

This service is responsible for:

- Exposing an HTTP API for order submission
- Validating incoming requests at a basic level
- Generating a **CorrelationId** for end-to-end workflow tracking
- Publishing `OrderAccepted` messages to a FIFO message queue
- Returning an immediate response to the client

This service does **not**:
- Persist data
- Perform business logic
- Interact with the database
- Block waiting for downstream processing

---

## Architectural Role

The Order Accept Service acts as a **boundary between synchronous client traffic and asynchronous backend processing**.

By immediately publishing an event and returning a response, it ensures:

- Low latency for client requests
- Protection of backend systems from traffic spikes
- Loose coupling between ingestion and processing layers

This design follows event-driven architecture principles and supports horizontal scalability.

---

## Workflow Overview

1. A client submits an order via HTTP.
2. The service validates the request.
3. A **CorrelationId** is generated.
4. An `OrderAccepted` event is published to the message queue.
5. The service responds immediately to the client.

The client does **not** wait for order processing to complete.

---

## Correlation Model

This service generates the **CorrelationId**, which is used to:

- Track the order across asynchronous components
- Correlate logs and traces
- Link backend processing with WebSocket notifications

The business identifier (`OrderId`) is intentionally **not generated here**.

| Identifier     | Purpose                           |
|----------------|-----------------------------------|
| CorrelationId  | Technical workflow correlation    |
| OrderId        | Generated later by the database   |

---

## Messaging

- Publishes messages to a **FIFO message queue**
- Azure Service Bus is used in production
- A local broker may be used for development purposes

This service does not subscribe to any messages.

---

## Design and Code Organization

This service follows **Clean Architecture** principles:

- The API layer depends on abstractions, not implementations
- Business rules are isolated from infrastructure concerns
- External dependencies (messaging, logging) are injected via interfaces

The codebase adheres to **SOLID principles** to ensure:
- Testability
- Clear separation of responsibilities
- Ease of change without cascading impact

## Domain Model

The Order Accept Service applies **Domain-Driven Design** at a lightweight level.

- It defines clear domain contracts for incoming orders
- No business rules or invariants are enforced here
- The domain is intentionally minimal, as this service acts only as an ingestion boundary

Core business logic is deferred to downstream processing services.

## Stateless Design

The Order Accept Service is fully **stateless**:

- No in-memory session state
- No persistence
- No dependency on downstream availability

This allows:
- Horizontal scaling
- Safe retries
- Resilience to partial outages

---

## Security and Authorization

This service does not perform authentication or authorization directly.

Security responsibilities are handled at the **API Gateway layer**, including:
- JWT validation
- Token issuer and audience verification
- Rate limiting and traffic protection

The service trusts forwarded identity claims and operates under a zero-trust internal model.

Authorization decisions, if required, are based on claims propagated from the gateway.

---

## Failure Handling

- If message publishing fails, the request is rejected
- No partial state is created
- Clients may safely retry requests

Basic resiliency mechanisms such as timeouts and circuit breaking
are applied around external infrastructure dependencies to prevent resource exhaustion.

---

## Technology Stack

- **.NET 8 / ASP.NET Core**
- Minimal APIs
- Messaging abstractions (Azure Service Bus in production)
- OpenTelemetry-compatible logging and tracing
- Docker

---

## API Documentation (OpenAPI)

In development and test environments, this service exposes an interactive **Swagger UI**
to facilitate exploration and validation of the HTTP API.


- The API contract is defined using **OpenAPI**
- Swagger UI is enabled only in non-production environments
- The OpenAPI specification can be used for:
    - Manual testing
    - Contract validation
    - Client generation
    - Integration with API gateways


In production environments, the service relies on the API Gateway
as the primary interface and governance layer.

---

## Logging

The service uses **structured logging** with correlation-aware context.

- Logs are emitted using the standard .NET logging abstractions
- The `CorrelationId` is included in log scopes for end-to-end traceability
- Logs are designed to be consumed by centralized logging platforms

Logging focuses on **diagnostic value**, not verbose tracing of normal execution paths.

---

## Telemetry and Observability

The service emits telemetry using **OpenTelemetry** standards.

- Distributed traces propagate the `CorrelationId` across service boundaries
- Metrics and traces are emitted in a vendor-neutral format
- Telemetry data can be consumed by multiple backends (e.g. Azure Monitor, Grafana)

Observability focuses on **system behavior and flow tracking**, not on low-level infrastructure metrics.

---

## Health Checks

This service exposes basic health check endpoints for container orchestration platforms.


- **Liveness**: indicates whether the service process is running
- **Readiness**: indicates whether the service is ready to accept traffic


Health checks validate **service availability**, not downstream infrastructure.
External dependencies such as message brokers are intentionally excluded
to prevent cascading failures and unnecessary restarts.

---

## Testing Strategy

The service is covered by **unit tests** focusing on:

- Request validation
- CorrelationId generation
- Message publishing behavior
- Error handling paths

External dependencies (Service Bus, logging) are mocked using abstractions.

The goal is to validate **service behavior**, not infrastructure correctness.

---

## Integration Testing

This service includes a limited set of **integration tests** to validate:

- HTTP request handling
- Message publishing behavior
- Messaging contracts and serialization

Integration tests run against a local message broker (RabbitMQ),
used solely for development and validation purposes.

Azure Service Bus is used in production environments, as it does not provide a local development emulator.

---

## Out of Scope

This service intentionally does not handle:

- Database access
- Order processing logic
- Retry orchestration
- WebSocket communication

Those responsibilities belong to downstream services.

---

## Design Philosophy

This service prioritizes:

- Fast request handling
- Clear responsibility boundaries
- Loose coupling
- Operational safety

It is designed to remain simple, predictable, and easy to reason about under load.