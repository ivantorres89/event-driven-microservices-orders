# Contoso Shop Frontend (Angular SPA)

This is the demo **Angular SPA** for the Contoso Shop portfolio.

It calls `order-accept` to submit orders and connects to `order-notification` (SignalR) to display real-time workflow updates.

## Local URLs

- SPA: `http://localhost:4200`
- Order Accept API: `http://localhost:8081`
- Notifications (HTTPS): `https://localhost:5007`

## Key configuration

Edit:

- `src/environments/environment.ts`

Typical values:

- `orderAcceptApiBaseUrl`: `http://localhost:8081`
- `signalRBaseUrl`: `https://localhost:5007`

