# Runbook — Order Process Service

## Purpose

`order-process` is the **core transaction processor**:
- **Consumes**: `OrderAccepted`
- **Persists**: Order to SQL (system of record)
- **Updates**: Redis workflow status (`PROCESSING` → `COMPLETED`)
- **Publishes**: `OrderProcessed` for notifications

## Ownership

- **Owner**: `group:platform`

## Quick links

- Source: https://github.com/ivantorres89/event-driven-microservices-orders/tree/main/services/order-process
- RabbitMQ UI (local): `http://localhost:15672`
- Jaeger (local): `http://localhost:16686`

## SLIs / SLOs (portfolio defaults)

| SLI | Target SLO | Notes |
|---|---:|---|
| Processing success rate | 99.9% | % messages that end in `COMPLETED` |
| End-to-end processing time | p95 ≤ 5s | `OrderAccepted` → `OrderProcessed` |
| Retry exhaustion rate | < 0.1% | Messages exceeding max retries |

## Dependencies

- SQL (`resource:sql-database`) — business persistence
- Redis (`resource:redis`) — transient status
- Broker (`resource:message-broker`) — inbound/outbound queues

### Queues

- Inbound: `order.accepted`
- Outbound: `order.processed`

(Local equivalents are RabbitMQ queues; cloud is Azure Service Bus queues.)

## Failure modes & symptoms

### 1) Consumer lag / backlog grows
Causes:
- SQL latency / deadlocks
- Redis timeouts
- Under-provisioned CPU/memory
- Poison messages causing repeated retries

Signals:
- `order.accepted` queue depth grows
- UI stuck in `ACCEPTED` / `PROCESSING`

### 2) SQL failures (transaction issues)
Signals:
- Exceptions on insert/update
- Elevated duration on SQL calls
- Deadlock errors

### 3) Poison message / schema mismatch
Signals:
- Same message repeatedly retried
- “Deserialization failed” / validation errors
- No progress in queue depth

## Triage checklist

1. **Queue depth + consumer rate**
   - Local: RabbitMQ UI queue depth and “consumers” count
   - Cloud: Service Bus metrics (Active Messages)
2. **SQL health**
   - Local: `docker compose logs sql`
   - Cloud: Azure SQL metrics (DTU/CPU, deadlocks)
3. **Redis health**
   - Connection timeouts, circuit breaker open events
4. **Traces**
   - Find a trace starting at `order-accept` and confirm downstream spans.
5. **Logs**
   - Look for retry loops and permanent failures.

## Recovery playbooks

### Scale out workers

If backlog grows due to throughput limits:

- AKS: increase replicas:
  ```bash
  kubectl scale deploy/order-process -n contoso-orders --replicas=3
  ```
- Watch:
  - queue depth decreases
  - processing time decreases

### Mitigate SQL contention

- Verify indexes (portfolio note: add as evolution)
- Reduce batch size / concurrency
- Consider a retry policy for transient SQL errors

### Handle poison messages

If a specific message always fails:
- Move it to a **dead-letter** (cloud) or a **quarantine queue** (local) (portfolio assumption)
- Fix the schema/validation
- Reprocess if safe

### Rollback

```bash
kubectl rollout undo deploy/order-process -n contoso-orders
```

## Local smoke test

1. Start stack
2. Place an order in the SPA
3. Confirm:
   - inbound queue receives a message
   - outbound queue receives `OrderProcessed`
   - SQL contains a new order row
   - Redis status moves to `COMPLETED`

