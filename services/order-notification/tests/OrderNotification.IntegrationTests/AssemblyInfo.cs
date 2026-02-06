using Xunit;

// Integration tests share RabbitMQ + Redis.
// Disable parallelization to avoid cross-test interference (queues, shared Redis keys).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
