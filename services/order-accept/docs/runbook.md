# Runbook — Order Accept Service

## Purpose

`order-accept` is the low-latency **ingress API** for order submission. It should respond quickly, publish an event, and **never block on downstream processing**.

## Ownership

- **Owner**: `group:platform`
- **On-call**: Platform (portfolio assumption)
- **Escalation**: Platform → Domain squad owning Orders (if this were a real org)

## Quick links

- Source: https://github.com/ivantorres89/event-driven-microservices-orders/tree/main/services/order-accept
- Local Swagger: `http://localhost:8081/swagger`
- Local RabbitMQ UI: `http://localhost:15672` (`guest/guest`)
- Jaeger UI (traces): `http://localhost:16686`

## SLIs / SLOs (portfolio defaults)

These are sensible defaults you can refine once you have real traffic.

| SLI | Target SLO | Notes |
|---|---:|---|
| Availability (2xx/3xx) | 99.9% | Measured at ingress / APIM |
| p95 latency | ≤ 250 ms | Includes auth + enqueue |
| Publish success rate | 99.99% | `OrderAccepted` published to broker |
| Error budget | 0.1% / 30d | |

## Dependencies

### Runtime dependencies

- **Redis** (`resource:redis`) — transient workflow state
- **Message broker** (`resource:message-broker`) — `OrderAccepted` queue/topic
- **SQL** (`resource:sql-database`) — not used directly for business persistence, but can be used for lookups/validation in some evolutions

### Local equivalents (Docker Compose)

- Redis: `localhost:6379`
- RabbitMQ: `localhost:5672` (+ UI on `15672`)
- SQL Server: `localhost:1433`

## Failure modes & symptoms

### 1) 5xx spikes / timeouts
Typical causes:
- Redis unreachable (timeouts, connection refused)
- Broker unreachable (RabbitMQ down / credentials)
- Thread pool starvation due to overload

What you’ll see:
- Increased p95 latency
- 500 responses (problem+json)
- Downstream queues stop receiving `OrderAccepted`

### 2) Requests succeed but orders “never progress”
Typical causes:
- Broker publish succeeded but consumer (`order-process`) is down
- Consumer is poisoned by a bad message and retries forever
- Redis TTL too short for the UX expectation

What you’ll see:
- `order-accept` returns CorrelationId but UI stays stuck at `ACCEPTED`

## Triage checklist

1. **Is the API up?**
   - Local: open Swagger or hit a cheap endpoint.
2. **Check Redis**
   - Local: `docker compose ps redis`
   - In AKS: `kubectl get pods -n contoso-orders`
3. **Check the broker**
   - Local: RabbitMQ UI queues & publish rates.
4. **Trace an example request**
   - Jaeger: search for service `order-accept` and follow the trace into `order-process`.
5. **Check logs**
   - Look for Redis/broker connection exceptions and retry storms.

## Recovery playbooks

### Redis outage

**Goal:** restore ability to write transient workflow state.

- Verify Redis health.
- If Redis is down:
  - Local: `docker compose restart redis`
  - AKS: restart Redis (if self-hosted) or validate Azure Cache status.
- Once Redis is back:
  - Confirm successful writes by submitting a test order and checking UI progression.

### Broker outage

**Goal:** restore ability to publish `OrderAccepted`.

- Local: `docker compose restart rabbitmq`
- Azure: validate Service Bus namespace + queue health, auth rules, and networking.
- Confirm queue gets new messages after a test order.

### Hotfix rollback (AKS)

If a deployment causes regressions:

1. Identify the previous stable image tag.
2. Roll back:
   ```bash
   kubectl rollout undo deploy/order-accept -n contoso-orders
   ```
3. Verify:
   - p95 latency normalizes
   - error rate returns to baseline

## Safe-to-run local smoke test

1. Start stack:
   ```bash
   docker compose up -d --build
   ```
2. Submit an order from the SPA or via Swagger (`/swagger`).
3. Observe:
   - RabbitMQ `order.accepted` receives a message
   - Jaeger trace spans across services
   - SPA receives status updates via SignalR

## Notes

- This service is intentionally **thin**. Keep business logic in downstream processors.
- Treat Redis as **ephemeral** (never a system of record).

