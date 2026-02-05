# Local stack (Docker Compose)

This folder contains the **local development** setup for running the full Contoso Orders demo end-to-end with **Docker Compose**, including **local HTTPS** for `order-notification` (REST + SignalR over WSS).

The recommended workflow is intentionally **environment-agnostic**:

- Start: `docker compose up -d --build`
- Stop:  `docker compose down`

## Prerequisites

- Docker Desktop (or Docker Engine)
- Docker Compose v2 (`docker compose ...`)
- .NET SDK (required to create/trust the HTTPS dev certificate via `dotnet dev-certs`)

## Ports (host)

- SPA: `http://localhost:4200`
- order-notification (HTTP): `http://localhost:5006`
- order-notification (HTTPS): `https://localhost:5007` (recommended)
- RabbitMQ UI: `http://localhost:15672` (guest/guest)
- Jaeger UI: `http://localhost:16686`

> Ports/passwords are defined in `infra/local/.env` (create it from `.env.example`).

> For SQL Server, make sure `SA_PASSWORD` is set (and matches what your apps/tests use to connect).

## Local HTTPS (order-notification)

`order-notification` exposes **both** HTTP and HTTPS:

- container `8080` -> host `5006` (HTTP)
- container `8081` -> host `5007` (HTTPS)

For HTTPS, Kestrel expects a **PFX** certificate mounted at:

- `infra/local/certs/contoso-devcert.pfx`
- password: `HTTPS_CERT_PASSWORD` (from `infra/local/.env`)

### One-time setup (per developer machine)

1) **Create/export the dev certificate PFX** for Docker

**macOS / Linux**
```bash
./infra/local/ensure-devcert.sh
```

**Windows (PowerShell)**
```powershell
.\infra\local\ensure-devcert.ps1
```

This generates:
- `infra/local/certs/contoso-devcert.pfx`

2) **Trust the dev certificate** (for browser / Postman)

```bash
dotnet dev-certs https --trust
```

> If you are prompted for permissions (especially on Windows/macOS), accept them.

### Start / Stop (environment-agnostic)

From the **repo root**:

Start:
```bash
docker compose up -d --build
```

Stop:
```bash
docker compose down
```

## Smoke tests

```bash
curl -vk https://localhost:5007/healthz
curl -vk https://localhost:5007/dev/token \
  -H "Content-Type: application/json" \
  -d '{"userId":"CUST-0001"}'
```

## Common pitfalls / troubleshooting

- **ERR_CONNECTION_REFUSED** when calling `https://localhost:5006/...`  
  Port **5006 is HTTP**. Use **`https://localhost:5007`** for HTTPS.

- **NET::ERR_CERT_AUTHORITY_INVALID** (browser) / `SSL certificate problem` (curl)  
  Run `dotnet dev-certs https --trust` and restart the browser.

- `order-notification` container keeps restarting  
  Most often the PFX is missing or the password is incorrect. Confirm:
  - `infra/local/certs/contoso-devcert.pfx` exists
  - `HTTPS_CERT_PASSWORD` in `infra/local/.env` matches the export password
