using OrderNotification.Shared.Correlation;

namespace OrderNotification.Shared.Workflow;

public static class WorkflowRedisKeys
{
    public static string OrderStatus(CorrelationId correlationId) => $"order:status:{correlationId}";

    /// <summary>
    /// CorrelationId -&gt; UserId mapping used to route SignalR notifications.
    ///
    /// Written by <c>order-accept</c> when the order is accepted (source for user routing).
    /// Refreshed by <c>order-process</c> when advancing workflow status to prevent TTL expiry.
    /// </summary>
    public static string OrderUserMap(CorrelationId correlationId) => $"order:map:{correlationId}";
}
