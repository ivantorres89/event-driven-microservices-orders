# Local stack (Docker Compose)

This folder contains the **local development** setup for running the full Contoso Orders demo end-to-end with **Docker Compose**.

## Prerequisites

- Docker Desktop (or Docker Engine)
- Docker Compose v2 (`docker compose ...`)
- .NET SDK (only needed to export the HTTPS dev certificate via `dotnet dev-certs`)

## Ports (host)

- SPA: `http://localhost:4200`
- order-notification (HTTP): `http://localhost:5006`
- order-notification (HTTPS): `https://localhost:5007` (recommended)
- RabbitMQ UI: `http://localhost:15672` (guest/guest)
- Jaeger UI: `http://localhost:16686`

> Ports/passwords are defined in `infra/local/.env` (created from `.env.example`).

## Start / Stop (recommended)

### macOS / Linux

```bash
./infra/local/up.sh --build
```

Stop:

```bash
./infra/local/down.sh
```

### Windows (PowerShell)

```powershell
.\infra\local\up.ps1 -Build
```

Stop:

```powershell
.\infra\local\down.ps1
```

## Local HTTPS (order-notification)

The scripts above will:
1) Export a dev HTTPS certificate to `infra/local/certs/contoso-devcert.pfx` (via `dotnet dev-certs`)
2) Mount it into the `order-notification` container
3) Start Kestrel with:
   - HTTP on container `8080` -> host `5006`
   - HTTPS on container `8081` -> host `5007`

If your browser rejects the cert, run:

```bash
dotnet dev-certs https --trust
```

More details: see `infra/local/HTTPS.md`.

## Smoke tests

```bash
curl -vk https://localhost:5007/healthz
curl -vk https://localhost:5007/dev/token \
  -H "Content-Type: application/json" \
  -d '{"userId":"CUST-0001"}'
```
