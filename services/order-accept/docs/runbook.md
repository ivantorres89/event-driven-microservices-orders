# Runbook — order-accept

**Owner:** group:platform  
**On-call / Escalation:** #platform-oncall  
**Environments:** local | dev | test | staging | prod  

> This runbook is intentionally pragmatic, safe defaults and recommended checks are provided.

---

## 1) Service overview
**order-accept** is the **HTTP entry point** for Contoso Orders. It serves the SPA-facing API, accepts order submissions quickly, initializes workflow state in Redis, and publishes an `OrderAccepted` integration event to the broker for asynchronous processing.

### What this service owns
- HTTP API (products + order submission)
- Workflow correlation (`CorrelationId`)
- Initial Redis workflow state (`ACCEPTED`) + correlation mapping (userId)
- Publishing `order.accepted` messages

### What this service does NOT own
- Durable order processing / business transaction (handled by `order-process`)
- Real-time delivery (handled by `order-notification`)
- Identity provider (only validates JWT)

## 2) Runtime & configuration
### Local (Docker Compose)
- **HTTP base URL:** `http://localhost:8081`
- **Container port:** 8080 (mapped to host 8081)
- **Dependencies:**
  - Redis: `ConnectionStrings__Redis=redis:6369`
  - SQL Server (local OLTP): `ConnectionStrings__Contoso=Server=sql,1433;Database=contoso;...`
  - RabbitMQ (local broker): `RabbitMQ__QueueName=order.accepted`

### Cloud (AKS / Azure)
- **Namespace:** `contoso-orders`
- **Deployment:** `order-accept` (replicas: 2 in `infra/k8s/base/20-order-accept-deployment.yaml`)
- **Service:** `order-accept` (ClusterIP: 8080)
- **Ingress routing:** `/` → `order-accept` (see `infra/k8s/base/50-ingress.yaml`)
- **Dependencies (expected):**
  - Azure Service Bus queue: `order.accepted`
  - Azure Cache for Redis (workflow state)
  - Azure SQL Database (product + order read models)

### Critical env vars (names)
- `ConnectionStrings__Redis`
- `ConnectionStrings__Contoso`
- `WorkflowState__Ttl` (default `00:30:00`)
- Local broker: `RabbitMQ__ConnectionString`, `RabbitMQ__QueueName`
- Cloud broker: `AzureServiceBus__ConnectionString`, `AzureServiceBus__QueueName`

## 3) Health checks
- **AKS probes:** `GET /health` (readiness + liveness in manifests)
- **What it means:** process is running and can accept HTTP traffic (deep dependency checks are intentionally shallow to avoid restart storms).

## 4) Key data (Redis keys)
> Redis is ephemeral (TTL-based). SQL is the system of record.

- `order:status:{CorrelationId}` → `ACCEPTED` (set by this service)
- `order:map:{CorrelationId}` → `{userId}` (used later by notification fan-out)
- TTL is controlled by `WorkflowState__Ttl` (default `00:30:00`)

## 5) First 10 minutes triage checklist
1. **Is it only this service?** Check error rate and latency for the API endpoints.
2. **Was there a rollout?** `kubectl rollout status deploy/order-accept`
3. **Check pod health/events:** CrashLoopBackOff, OOMKilled, readiness failures.
4. **Check logs/traces:** look for correlationId, broker publish errors, Redis timeouts, SQL errors.
5. **Check dependencies quickly**
   - Redis: connection errors/timeouts
   - Broker: publish failures / auth errors
   - SQL: timeouts / connection pool exhaustion (esp. product endpoints)

## 6) Common incidents & playbooks

### 6.1 Spike in 5xx (API errors)
**Symptoms**
- `/api/products` or `/api/orders` returning 5xx
- Increased latency + thread pool starvation

**Checks**
- `kubectl -n contoso-orders logs deploy/order-accept --since=15m`
- Look for: SQL timeouts, Redis timeouts, serialization/validation exceptions, broker publish failures.

**Mitigations**
- **Rollback** if correlated with a recent deploy: `kubectl rollout undo deploy/order-accept`
- **Scale out** if CPU-bound (watch SQL connection pressure)
- **Degrade non-critical endpoints** (portfolio suggestion): temporarily disable heavy product queries or expensive filters

### 6.2 Orders are accepted but never processed
**Symptoms**
- Clients get a `CorrelationId` but status never moves past `ACCEPTED`
- `order.accepted` backlog increases

**Checks**
- Broker publish errors in `order-accept` logs
- Broker queue depth (RabbitMQ UI locally; Service Bus metrics in Azure)
- `order-process` consumer health (see `order-process` runbook)

**Mitigations**
- If publish failing: fix broker connection string/credentials, restart pods
- If publish OK but consumer stuck: scale/restart `order-process`

### 6.3 Redis unavailable / high latency
**Symptoms**
- Errors writing `order:status:*` and `order:map:*`
- Notifications cannot route later

**Mitigations**
- Fail fast to avoid cascading retries
- If in AKS: confirm Redis endpoint/DNS, network policies, secret values
- Short-term: accept requests but log “workflow state unavailable” (only if UX impact acceptable)

## 7) Operational notes
- **Idempotency:** downstream consumers assume at-least-once delivery; message payloads must be safe under retries.
- **CorrelationId:** treat it as the primary workflow key; ensure it is logged for every request and publish.

## Appendix — useful commands

### Kubernetes (AKS)
```bash
# Workloads
kubectl -n contoso-orders get deploy,pod,svc,ingress,job,hpa

# Describe / events
kubectl -n contoso-orders describe pod <pod>
kubectl -n contoso-orders get events --sort-by=.metadata.creationTimestamp | tail -n 50

# Logs
kubectl -n contoso-orders logs deploy/<deployment> --since=15m
kubectl -n contoso-orders logs <pod> -c <container> --since=15m

# Rollout
kubectl -n contoso-orders rollout status deploy/<deployment>
kubectl -n contoso-orders rollout undo deploy/<deployment>
kubectl -n contoso-orders rollout restart deploy/<deployment>
```

### Docker Compose (local)
```bash
docker compose ps
docker compose logs -f --tail=200 <service>
docker compose restart <service>
```

