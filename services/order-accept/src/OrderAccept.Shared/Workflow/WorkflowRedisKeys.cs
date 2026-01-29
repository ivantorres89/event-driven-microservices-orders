using OrderAccept.Shared.Correlation;

namespace OrderAccept.Shared.Workflow;

public static class WorkflowRedisKeys
{
    public static string OrderStatus(CorrelationId correlationId) => $"order:status:{correlationId}";
}
