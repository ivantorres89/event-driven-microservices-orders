export const environment = {
  production: false,

  // order-notification host (SignalR hub lives at /hubs/order-status)
  // Example: "https://localhost:5007"
  signalRBaseUrl: "https://localhost:5007",

  // order-accept API base URL used by DELETE /api/orders/{id}
  // Example: "https://localhost:5005"
  orderAcceptApiBaseUrl: "https://localhost:5005",

  // Demo user id (in Development backend: DevAuthenticationHandler treats Bearer token as userId)
  // IMPORTANT: backend must accept access_token in query for WebSockets (recommended patch).
  demoUserId: "contoso-user-001",

  // For demo without backend, you can enable mocks:
  useMocks: true
};
