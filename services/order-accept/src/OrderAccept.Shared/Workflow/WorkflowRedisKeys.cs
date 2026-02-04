using OrderAccept.Shared.Correlation;

namespace OrderAccept.Shared.Workflow;

public static class WorkflowRedisKeys
{
    public static string OrderStatus(CorrelationId correlationId) => $"order:status:{correlationId}";

    /// <summary>
    /// Short-lived mapping from CorrelationId -> authenticated UserId (JWT subject).
    /// Used by the notification service to route SignalR messages without embedding user ids in broker messages.
    /// </summary>
    public static string OrderUserMap(CorrelationId correlationId) => $"order:map:{correlationId}";

    /// <summary>
    /// Optional mapping from CorrelationId -> OrderId once persistence has occurred.
    /// </summary>
    public static string OrderId(CorrelationId correlationId) => $"order:id:{correlationId}";

    /// <summary>
    /// Optional hint for the most recent correlation for a given user.
    /// </summary>
    public static string OrderLastForUser(string userId) => $"order:last:{userId}";
}
