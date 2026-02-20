# Runbook — order-process

**Owner:** group:platform  
**On-call / Escalation:** #platform-oncall  
**Environments:** local | dev | test | staging | prod  
**Last reviewed:** 2026-02-20 • **Next review:** 2026-02-20 (portfolio baseline)

> This runbook is intentionally pragmatic and copy/paste friendly. When details are unknown (portfolio repo), safe defaults and recommended checks are provided.

---

## 1) Service overview
**order-process** is a **background worker** that consumes `order.accepted` events, executes the OLTP transaction in SQL, updates transient workflow state in Redis (`PROCESSING` → `COMPLETED`), and publishes `order.processed` for downstream notifications.

### What this service owns
- Durable order persistence (SQL is the system of record)
- Idempotent consumption of accepted orders (at-least-once safe)
- Workflow state updates in Redis
- Publishing `order.processed`

### What this service does NOT own
- Public HTTP API for clients
- WebSockets / SignalR
- Identity / auth (edge responsibility)

## Quick links
- **Repo root:** `README.md` • `Workflow.md` • `architecture/README_arch.md`
- **Local stack docs:** `infra/local/README.md`
- **Cloud (AKS + Terraform) docs:** `infra/README-cloud.md`
- **CI (GitHub Actions):** `.github/workflows/ci.yml`
- **Local observability:** Jaeger UI: `http://localhost:16686` (traces), RabbitMQ UI: `http://localhost:15672` (guest/guest)



## 2) Runtime & configuration
### Local (Docker Compose)
- Runs as a worker container (no public ports)
- Dependencies:
  - SQL Server: `ConnectionStrings__Contoso=Server=sql,1433;Database=contoso;...`
  - Redis: `ConnectionStrings__Redis=redis:6379`
  - RabbitMQ:
    - inbound: `order.accepted`
    - outbound: `order.processed`
- **Migration job:** `order-process-migrations` must complete successfully before the worker starts.

### Cloud (AKS / Azure)
- **Namespace:** `contoso-orders`
- **Deployment:** `order-process` (replicas: 3 in `infra/k8s/base/30-order-process-deployment.yaml`)
- **Migration job:** `order-process-migrations` (see `infra/k8s/base/10-order-process-migrations-job.yaml`)
- Dependencies:
  - Azure SQL Database
  - Azure Cache for Redis
  - Azure Service Bus queues: `order.accepted` and `order.processed`

### Critical env vars (names)
- `ConnectionStrings__Contoso`
- `ConnectionStrings__Redis`
- `WorkflowState__Ttl` (default `00:30:00`)
- Local broker: `RabbitMQ__InboundQueueName`, `RabbitMQ__OutboundQueueName`, `RabbitMQ__MaxProcessingAttempts`
- Cloud broker: `AzureServiceBus__InboundQueueName`, `AzureServiceBus__OutboundQueueName`

## 3) Key data (Redis keys)
- `order:status:{CorrelationId}` → `PROCESSING` → `COMPLETED`
- `order:id:{CorrelationId}` → `{OrderId}` (if enabled)
- TTL is controlled by `WorkflowState__Ttl` (default `00:30:00`)

## 4) First 10 minutes triage checklist
1. **Is backlog growing?** Check `order.accepted` depth + oldest message age.
2. **Are pods running?** `kubectl -n contoso-orders get pod -l app.kubernetes.io/name=order-process`
3. **Any DB errors?** look for SQL deadlocks/timeouts/connection exhaustion.
4. **Any poison messages?** repeated processing attempts, DLQ growth.
5. **Migrations job status:** failed migrations will block persistence.

## 5) Common incidents & playbooks

### 5.1 Queue backlog increasing (orders stuck in ACCEPTED)
**Checks**
- Worker logs for exceptions and retry loops
- SQL metrics (CPU, DTU/vCore, connection count, deadlocks)
- Redis latency (status updates)

**Mitigations**
- **Scale out worker replicas** (watch SQL and Redis saturation)
- **Rollback** if a new release introduced failures
- **Throttle intake** at gateway/API if DB is saturated

### 5.2 SQL timeouts / deadlocks / connection exhaustion
**Symptoms**
- Slow processing, many retries, increasing DLQ
- Exceptions in logs

**Mitigations**
- Reduce concurrency (replicas) temporarily to stabilize DB
- Scale Azure SQL (vCore/DTU) for the duration of the incident
- If caused by a query change: rollback/hotfix

### 5.3 DLQ growth / poison messages
**Checks**
- Identify message type and correlationId
- Inspect failure reason from logs
- Verify schema/contract compatibility (producer vs consumer)

**Mitigations**
- Fix root cause, then re-drive DLQ messages (procedure depends on broker)
- If safe: implement idempotency de-dupe keying on CorrelationId + payload hash

### 5.4 Migrations job failing
**Symptoms**
- `order-process-migrations` job in `Error` / `BackoffLimitExceeded`
- Worker cannot persist

**Mitigations**
- Check DB connection string secret
- Validate SQL firewall / private endpoint rules (cloud)
- Re-run job after fix:
  ```bash
  kubectl -n contoso-orders delete job order-process-migrations
  # re-apply using your preferred mechanism (GitOps/helm/kustomize)
  ```

## 6) Operational notes
- **Idempotency:** consumption must tolerate duplicate deliveries.
- **Transaction boundary:** SQL write should be atomic; publish should occur after commit (or via outbox in a production-hardened variant).

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

