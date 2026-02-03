export const environment = {
  production: false,

  // order-notification host (SignalR hub lives at /hubs/order-status)
  // Example: "https://localhost:5007"
  signalRBaseUrl: "https://localhost:5007",

  // order-accept API base URL used by DELETE /api/orders/{id}
  // Example: "https://localhost:5005"
  orderAcceptApiBaseUrl: "https://localhost:5005",

  // Optional: default user id prefilled in the Login screen (Development)
  // IMPORTANT: backend must accept access_token in query for WebSockets (recommended patch).
  defaultUserId: "contoso-user-001",

  // For demo without backend, you can enable mocks:
  useMocks: true
};
