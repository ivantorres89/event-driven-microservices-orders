# Runbook — order-notification

**Owner:** group:platform  
**On-call / Escalation:** #platform-oncall  
**Environments:** local | dev | test | staging | prod  
**Last reviewed:** 2026-02-20 • **Next review:** 2026-02-20 (portfolio baseline)

> This runbook is intentionally pragmatic and copy/paste friendly. When details are unknown (portfolio repo), safe defaults and recommended checks are provided.

---

## 1) Service overview
**order-notification** is the **real-time edge** of the workflow. It hosts a **SignalR hub** and consumes `order.processed` to push order status updates to the correct user over WebSockets (WSS), supporting multi-pod scale-out via a Redis backplane (no sticky sessions).

### What this service owns
- SignalR hub: `/hubs/order-status`
- WebSocket session registry in Redis (userId ↔ connectionId)
- Notification fan-out using Redis mapping `order:map:{CorrelationId}`
- Consuming `order.processed` and emitting hub messages

### What this service does NOT own
- Order acceptance / persistence
- Durable business state (Redis is ephemeral)
- Token issuance (except dev convenience endpoint locally)

## Quick links
- **Repo root:** `README.md` • `Workflow.md` • `architecture/README_arch.md`
- **Local stack docs:** `infra/local/README.md`
- **Cloud (AKS + Terraform) docs:** `infra/README-cloud.md`
- **CI (GitHub Actions):** `.github/workflows/ci.yml`
- **Local observability:** Jaeger UI: `http://localhost:16686` (traces), RabbitMQ UI: `http://localhost:15672` (guest/guest)



## 2) Runtime & configuration
### Local (Docker Compose)
- **HTTP:** `http://localhost:5006`
- **HTTPS:** `https://localhost:5007` (recommended for WSS)
- Hub endpoint: `https://localhost:5007/hubs/order-status`
- **Dev token endpoint:** `POST /dev/token` (local/dev only; uses `DevJwt__SigningKey`)
- Dependencies:
  - Redis (session registry + backplane): `ConnectionStrings__Redis=redis:6379`
  - RabbitMQ queue: `order.processed`
- TLS cert for local HTTPS: mounted from `infra/local/certs`

### Cloud (AKS / Azure)
- **Namespace:** `contoso-orders`
- **Deployment:** `order-notification` (replicas: 2 in `infra/k8s/base/40-order-notification-deployment.yaml`)
- **Service:** `order-notification` (ClusterIP: 8080)
- **Ingress routing:** `/hubs/order-status` → `order-notification`
- Dependencies:
  - Azure Cache for Redis (backplane + session routing)
  - Azure Service Bus queue `order.processed`

### Critical env vars (names)
- `ConnectionStrings__Redis`
- `SignalR__ChannelPrefix` (e.g., `contoso-signalr`)
- Local broker: `RabbitMQ__ConnectionString`, `RabbitMQ__QueueName`
- Cloud broker: `AzureServiceBus__ConnectionString`, `AzureServiceBus__InboundQueueName`
- Local-only: `DevJwt__SigningKey`, `ASPNETCORE_Kestrel__Certificates__Default__Path`

## 3) Health checks
- **AKS probes:** 
  - `GET /health/ready` (readiness)
  - `GET /health/live` (liveness)
- Probes avoid deep dependency checks (Redis/broker) to prevent restart storms.

## 4) Key data (Redis keys)
### WebSocket session registry
- `ws:connections:{userId}` → set/list of `connectionId` (TTL aligned with session)
- `ws:connection:{connectionId}` → `{ userId, connectedAt }` (TTL)

### Order correlation routing
- `ws:session:{correlationId}` → `{ userId, connectionId(s) }` (TTL 30–60 minutes)

### Workflow state lookup
- `order:map:{correlationId}` → `{userId}` (written by `order-accept`)
- `order:status:{correlationId}` → `ACCEPTED | PROCESSING | COMPLETED`

## 5) First 10 minutes triage checklist
1. **Are clients failing to connect?** Check hub handshake errors, TLS, ingress timeouts.
2. **Are messages being consumed?** Check `order.processed` backlog + consumer logs.
3. **Is Redis healthy?** backplane + session lookups depend on it.
4. **Cross-pod delivery?** If only some clients get updates, suspect backplane/channel prefix.
5. **Check readiness/liveness:** if pods flap, investigate resource limits and startup errors.

## 6) Common incidents & playbooks

### 6.1 WSS connection failures (502/504, negotiation loops)
**Checks**
- Ingress settings for WebSockets (`proxy-read-timeout`, buffering off)
- Pod readiness (`/health/ready`) and endpoint membership
- Local dev: certificate export/trust steps in `infra/local/README.md`

**Mitigations**
- Restart rollout if pods stuck: `kubectl rollout restart deploy/order-notification`
- Increase ingress timeouts for long-lived WebSockets (already configured in repo)
- Force long polling in the SPA only as a temporary workaround (`SIGNALR_FORCE_LONG_POLLING=true` locally)

### 6.2 Notifications not delivered (but processing completes)
**Checks**
- `order:map:{CorrelationId}` exists and has not expired
- `ws:connections:{userId}` populated (client registered)
- Consumer is reading `order.processed`

**Mitigations**
- Increase workflow TTL (`WorkflowState__Ttl`) if expiry is too aggressive
- Ensure the SPA binds correlationId to the connection (hub contract)
- If Redis flaky: reduce dependency pressure (timeouts, retries) and stabilize Redis

### 6.3 Multi-pod issue: only some clients receive messages
**Likely cause**
- Redis backplane misconfigured (wrong endpoint or channel prefix) or Redis pub/sub issues.

**Checks**
- Confirm `SignalR__ChannelPrefix` is consistent across replicas
- Verify Redis connectivity from all pods

**Mitigations**
- Fix configuration, redeploy
- As a short-term workaround, reduce replicas (not recommended long-term)

### 6.4 `order.processed` backlog increasing
**Checks**
- Broker connectivity, auth errors
- Consumer exceptions / poison messages

**Mitigations**
- Scale out notification pods
- Fix contract/version mismatch, then re-drive DLQ

## 7) Operational notes
- Duplicate events are acceptable; sending the same notification twice is typically safer than dropping.
- Prefer correlationId-first troubleshooting (it ties together HTTP, broker, Redis, and hub delivery).

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

