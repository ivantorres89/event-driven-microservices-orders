using OrderNotification.Shared.Correlation;

namespace OrderNotification.Shared.Workflow;

public static class WorkflowRedisKeys
{
    public static string OrderStatus(CorrelationId correlationId) => $"order:status:{correlationId}";
}
