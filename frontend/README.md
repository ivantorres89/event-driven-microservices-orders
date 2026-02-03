# Contoso Shop (Angular Frontend)

A demo **Angular (standalone)** SPA for Contoso Shop for eLearning certification IT courses.

Included:
- “Microsoft-ish” layout: **top header + left navigation**
- Pages: **Products**, **Checkout**, **Orders**
- **Cart overlay** (not persisted)
- **SignalR** integration (order-notification) to receive order workflow notifications
- A lightweight **Login** page to simulate users (especially useful in Development)

---

## Prerequisites

- Node.js LTS (recommended >= 18)
- Angular CLI (optional but recommended)

Install dependencies:

```bash
npm install
```

---

## Configuration

Edit:

`src/environments/environment.ts`

Typical values:

- `signalRBaseUrl`: base URL of **order-notification** (where the SignalR Hub is hosted)
- `orderAcceptApiBaseUrl`: base URL of **order-accept** (REST API used to create/delete orders)
- (Optional) `useMocks`: when `true`, the UI uses local mocks for products/orders (demo without backend)

---

## Run locally

```bash
npm start
```

Angular will serve the SPA at `http://localhost:4200`.

---

## Login (IMPORTANT for Development)

### Why do we need Login here?

This frontend uses **SignalR UserIdentifier** to route notifications:

- the backend sends updates using `Clients.User(userId)`

Therefore, if the client is not “logged in” (i.e., `Context.UserIdentifier` is missing), **the Hub will abort the connection**.

### What does the Login page do?

The `/login` page lets you set a `userId` at runtime and stores it in `localStorage`.

- Key: `contoso.userId`
- In this demo, the `userId` is also used as the “access token” in Development

> **In Development:** the backend `DevAuthenticationHandler` interprets the **Bearer token as the userId**.

---

## SignalR Hub integration

- Hub URL: `GET {signalRBaseUrl}/hubs/order-status`
- Client calls:
  - `Ping()`
  - `RegisterOrder(correlationId)`
  - `GetCurrentStatus(correlationId)`
- Client receives:
  - `Notification(...)`

### Multi-tab / multi-window

Because the backend uses `Clients.User(userId)`, **all tabs** for the same user automatically receive the same notifications.

---

## Key point: WebSockets + Auth in browsers (Development)

In browsers, **WebSockets cannot set the `Authorization` header** during the WebSocket upgrade.
SignalR typically passes the token in the query string:

- `?access_token=...`

Recommended DEV approach:
- make sure the backend Dev auth accepts BOTH:
  - `Authorization: Bearer <token>`
  - and `access_token` query param (SignalR WebSockets)

### Recommended patch (backend, Development)

Inside `HandleAuthenticateAsync()`:

```csharp
string? userId = null;

// 1) Authorization header: Bearer <token>
if (Request.Headers.TryGetValue("Authorization", out var auth) && auth.Count > 0)
{
    var raw = auth.ToString();
    const string prefix = "Bearer ";
    if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        userId = raw[prefix.Length..].Trim();
}

// 2) Query ?access_token=... (SignalR WebSockets)
if (string.IsNullOrWhiteSpace(userId))
{
    userId = Request.Query["access_token"].ToString();
}

if (string.IsNullOrWhiteSpace(userId))
    return Task.FromResult(AuthenticateResult.Fail("Missing bearer token"));
```

With this patch you can use **WebSockets transport** reliably in Development.

> If the backend does not accept `access_token`, SignalR may fall back to other transports (e.g., LongPolling) or fail depending on your settings.

---

## Demo flow (end-to-end)

### 1) Products
When you enter **Products**, the app:
- ensures a `userId` exists (otherwise redirects to `/login`)
- connects to SignalR
- displays the products table (mocked or backend-driven depending on `useMocks`)

### 2) Cart (overlay)
- Add products and quantities
- Cart is in-memory only: if you close the tab, it is lost

### 3) Checkout
- Displays the cart as order lines
- On **Submit**:
  - creates an order (mock or backend)
  - stores the active `correlationId` in `localStorage`
  - shows a spinner/progress state
  - calls `RegisterOrder(correlationId)` so the backend can store `correlationId -> userId` in Redis (TTL 30 min)
  - the backend pushes status updates when consuming `order.processed`

### 4) Refresh / close tab and come back
The app restores:
- `userId` from `localStorage`
- the active `correlationId` (if any)
Then it reconnects to SignalR and:
- re-registers the correlationId
- calls `GetCurrentStatus(correlationId)`
- restores the spinner/state accordingly

### 5) Orders (and Delete)
- Shows the user orders (mock/localStorage in demo mode)
- Allows deleting an order via:
  - `DELETE {orderAcceptApiBaseUrl}/api/orders/{id}`

---

## Production notes (real auth)

- In production, `userId` should come from a stable claim (e.g., `sub` or `nameidentifier`) in a real JWT.
- This frontend is designed to support:
  - DEV: `token == userId` (DevAuth)
  - PROD: real JWT (same routing model, no UI changes)

---

## Quick troubleshooting

**1) “The Hub won’t connect”**
- Make sure you are logged in (`/login`)
- Verify `signalRBaseUrl`
- If you want WebSockets in DEV, apply the `access_token` patch shown above

**2) “No notifications arrive”**
- Ensure Checkout calls `RegisterOrder(correlationId)`
- Confirm backend stores `correlationId -> userId` in Redis
- Confirm `order.processed` messages contain the expected correlationId

---

## License
Internal demo (Contoso).
