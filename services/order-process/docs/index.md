# Order Process Service

`order-process` is a **background worker** that consumes `OrderAccepted` events, persists the business order to SQL, updates the transient workflow state in Redis, and publishes `OrderProcessed` for downstream notifications.

## Characteristics

- **No external HTTP API** (worker model)
- Scales horizontally (multiple replicas)
- Uses **retries + poison handling** patterns (portfolio assumption)

## Local execution

Run with the full stack:

```bash
docker compose up -d --build
```

Then verify RabbitMQ queues and the Jaeger trace graph.

