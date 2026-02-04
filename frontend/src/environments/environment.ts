export const environment = {
  production: false,

  // Fallback values (used if /assets/runtime-config.json is missing).
  // In Docker Compose, runtime-config.json is generated at container startup
  // from infra/local/.env (single source of truth for ports).
  signalRBaseUrl: "https://localhost:5007",

  // order-accept API base URL used by Products/Orders API calls
  orderAcceptApiBaseUrl: "http://localhost:8081",

  // Default user id prefilled in the Login screen (Development)
  defaultUserId: "CUST-0001",

  // SignalR transport override (Development). Leave false to prefer WebSockets.
  signalRForceLongPolling: false,

  // For demo without backend, you can enable mocks:
  useMocks: false
};
