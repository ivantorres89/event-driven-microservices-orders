# Runbook — Contoso Shop Frontend (Angular SPA)

## Purpose

The SPA provides the end-user UI:
- browse products
- checkout (submit order)
- watch order status change in real time via SignalR

## Ownership

- **Owner**: `group:platform`

## Quick links

- Source: https://github.com/ivantorres89/event-driven-microservices-orders/tree/main/frontend
- Local SPA: `http://localhost:4200`
- Back-end stack: `docker compose up -d --build`

## SLIs / SLOs (portfolio defaults)

| SLI | Target SLO | Notes |
|---|---:|---|
| Availability | 99.9% | CDN/Static hosting |
| Core Web Vitals | “Good” | LCP/INP/CLS tracked in RUM |
| API error rate surfaced | < 0.5% | UI should show actionable errors |

## Dependencies

- `order-accept` (REST) — submit orders
- `order-notification` (SignalR) — real-time updates
- Optional: auth provider / identity (portfolio: simulated login)

## Common issues

### 1) UI loads, but checkout fails
Symptoms:
- 4xx/5xx calling `order-accept`

Checks:
- Confirm `orderAcceptApiBaseUrl`
- Confirm Docker Compose is running
- Check CORS configuration on `order-accept`

### 2) “Connected” never happens / notifications don’t arrive
Symptoms:
- SignalR fails negotiate or WS upgrade

Checks:
- Confirm `signalRBaseUrl` points to **HTTPS** (`https://localhost:5007`)
- Ensure local dev cert exists and is trusted
- Confirm hub path: `/hubs/order-status`

### 3) Login required (UserIdentifier)
This demo routes notifications with `Clients.User(userId)`. If the SPA is not “logged in”, the Hub may abort or messages won’t route.

Fix:
- Use the demo login flow and keep `DEFAULT_USER_ID` consistent across runs (portfolio approach).

## Local run procedure

1. (Optional) create/ trust dev cert (for notification HTTPS):
   ```bash
   ./infra/local/ensure-devcert.sh
   dotnet dev-certs https --trust
   ```

2. Start backend stack:
   ```bash
   docker compose up -d --build
   ```

3. Start the SPA:
   ```bash
   cd frontend
   npm install
   npm start
   ```

4. Verify:
   - `http://localhost:4200` loads
   - Checkout creates an order and returns a CorrelationId
   - Status updates appear (ACCEPTED → PROCESSING → COMPLETED)

## Incident response notes (portfolio)

- If notifications are down, degrade gracefully: show polling or “refresh” CTA.
- If order submission is down, fail fast with a friendly error and retry guidance.

