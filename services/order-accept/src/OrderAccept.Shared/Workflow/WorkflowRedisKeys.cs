using OrderAccept.Shared.Correlation;

namespace OrderAccept.Shared.Workflow;

/// <summary>
/// Provides factory methods for generating Redis key names used in workflow-related operations.
/// </summary>
public static class WorkflowRedisKeys
{
    /// <summary>
    /// Generates a status key string for the specified order correlation identifier.
    /// </summary>
    /// <param name="correlationId">The correlation identifier associated with the order for which to generate the status key.</param>
    /// <returns>A string representing the status key for the specified order correlation identifier.</returns>
    public static string OrderStatus(CorrelationId correlationId)
        => $"order:status:{correlationId}";
}
