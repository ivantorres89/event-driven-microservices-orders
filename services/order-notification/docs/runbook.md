# Runbook — Order Notification Service

## Purpose

`order-notification` provides **real-time UX** for the async workflow:
- Maintains SignalR connections
- Resolves user/session routing via Redis
- Consumes `OrderProcessed`
- Emits `OrderStatusChanged` notifications to clients

## Ownership

- **Owner**: `group:platform`

## Quick links

- Source: https://github.com/ivantorres89/event-driven-microservices-orders/tree/main/services/order-notification
- Local HTTPS base URL: `https://localhost:5007`
- Health: `https://localhost:5007/healthz`
- RabbitMQ UI: `http://localhost:15672`
- Jaeger: `http://localhost:16686`

## SLIs / SLOs (portfolio defaults)

| SLI | Target SLO | Notes |
|---|---:|---|
| Hub availability | 99.9% | Measured at ingress (WSS) |
| Connection success rate | 99.5% | Negotiate + WS upgrade |
| Notification delivery latency | p95 ≤ 1s | `OrderProcessed` → client event |
| Reconnect success | ≥ 99% | Within 30s window |

## Dependencies

- Redis (`resource:redis`) — user/session registry + workflow status lookup
- Broker (`resource:message-broker`) — consumes `order.processed` (local queue)
- Optional edge: API Gateway / Ingress must support WebSockets

## Failure modes & symptoms

### 1) Clients cannot connect (WS handshake failures)
Causes:
- Ingress not configured for WebSockets
- TLS/devcert issues
- Auth issues (missing/invalid JWT)
- Sticky-session assumptions (should not exist here)

Signals:
- SPA shows “disconnected” and never receives updates
- Server logs show negotiate failures or aborted connections

### 2) Connected, but no notifications
Causes:
- Consumer not consuming `OrderProcessed`
- Redis missing user mapping (client didn’t register correlationId)
- UserIdentifier mismatch between SPA token and backend routing

Signals:
- Hub shows connections but no `OrderStatusChanged`

### 3) TLS / certificate errors (local)
Signals:
- Browser shows cert warnings
- `NET::ERR_CERT_AUTHORITY_INVALID`

Fix:
- Generate and trust dev cert (see below)

## Triage checklist

1. **Health**
   - `curl -vk https://localhost:5007/healthz`
2. **TLS**
   - Confirm cert exists: `infra/local/certs/contoso-devcert.pfx`
3. **Auth**
   - Generate a dev token:
     ```bash
     curl -k https://localhost:5007/dev/token \
       -H "Content-Type: application/json" \
       -d '{"userId":"CUST-0001"}'
     ```
4. **Broker**
   - Is `order.processed` queue receiving messages?
5. **Redis**
   - Is the correlation/user mapping being written?
6. **Traces**
   - Follow span from `order-process` publish to `order-notification` consume and hub emit.

## Recovery playbooks

### Local HTTPS / dev cert

One-time (per machine):

```bash
./infra/local/ensure-devcert.sh
dotnet dev-certs https --trust
```

Windows PowerShell:

```powershell
.\infra\local\ensure-devcert.ps1
dotnet dev-certs https --trust
```

Then restart:

```bash
docker compose restart order-notification
```

### Broker restart (local)

```bash
docker compose restart rabbitmq
```

### Redis restart (local)

```bash
docker compose restart redis
```

### Rollback (AKS)

```bash
kubectl rollout undo deploy/order-notification -n contoso-orders
```

## Operational notes

- Prefer **stateless** hub pods (no sticky sessions).
- Redis is allowed to lose state; client should reconnect and re-register correlationId.
- In production, prefer explicit connection limits and backpressure.

