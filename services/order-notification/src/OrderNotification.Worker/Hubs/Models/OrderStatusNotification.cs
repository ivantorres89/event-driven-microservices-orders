using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.Worker.Hubs.Models;

public sealed record OrderStatusNotification(
    CorrelationId CorrelationId,
    OrderWorkflowStatus Status,
    long? OrderId);
