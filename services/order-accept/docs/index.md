# Order Accept Service

The **Order Accept Service** is the **entry point** of the Contoso Orders platform.

It exposes a REST API that receives checkout requests from the SPA, generates a **CorrelationId**, stores transient workflow status in **Redis**, and publishes an **`OrderAccepted`** integration event to the broker for asynchronous processing.

## Where it fits

- **Upstream**: Angular SPA (`frontend`) and any external clients.
- **Downstream**: `order-process` (consumes `OrderAccepted`) and `order-notification` (shows progress to the user).

## Local endpoints (Docker Compose)

- Base URL: `http://localhost:8081`
- Swagger UI: `http://localhost:8081/swagger`
- Health (requires auth): `/health/live`, `/health/ready`

> For local development, start the full stack with `docker compose up -d --build` from repo root.

## Docs & Runbook

- See the **Runbook** page for on-call procedures, troubleshooting and recovery steps.

