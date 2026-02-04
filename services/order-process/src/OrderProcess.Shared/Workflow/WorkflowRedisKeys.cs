using OrderProcess.Shared.Correlation;

namespace OrderProcess.Shared.Workflow;

public static class WorkflowRedisKeys
{
    public static string OrderStatus(CorrelationId correlationId) => $"order:status:{correlationId}";
    public static string OrderUserMap(CorrelationId correlationId) => $"order:map:{correlationId}";
}
