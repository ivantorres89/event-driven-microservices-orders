# order-process

Background worker: consumes OrderAccepted, persists to SQL, updates Redis status, publishes OrderProcessed.


## Runbook
- See `runbook.md`.
