using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrderNotification.Application.Abstractions;
using OrderNotification.Shared.Correlation;

namespace OrderNotification.Worker.Hubs;

[Authorize]
public sealed class OrderStatusHub : Hub<IOrderStatusClient>
{
    private readonly IOrderCorrelationRegistry _correlationRegistry;
    private readonly IOrderWorkflowStateQuery _workflowQuery;
    private readonly ILogger<OrderStatusHub> _logger;

    public OrderStatusHub(
        IOrderCorrelationRegistry correlationRegistry,
        IOrderWorkflowStateQuery workflowQuery,
        ILogger<OrderStatusHub> logger)
    {
        _correlationRegistry = correlationRegistry;
        _workflowQuery = workflowQuery;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        if (string.IsNullOrWhiteSpace(Context.UserIdentifier))
        {
            _logger.LogWarning("SignalR connection aborted because Context.UserIdentifier is missing.");
            Context.Abort();
            return;
        }

        _logger.LogInformation("SignalR connected. ConnectionId={ConnectionId} UserId={UserId}", Context.ConnectionId, Context.UserIdentifier);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR disconnected. ConnectionId={ConnectionId} UserId={UserId}", Context.ConnectionId, Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Lightweight health call for clients to verify connectivity.
    /// </summary>
    public Task Ping() => Task.CompletedTask;

    /// <summary>
    /// Registers the current authenticated user as the owner of the provided correlationId.
    /// Stores a CorrelationId -> UserId mapping in Redis with the workflow TTL.
    /// </summary>
    public async Task RegisterOrder(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(Context.UserIdentifier))
            throw new HubException("Missing user identifier");

        if (!Guid.TryParse(correlationId, out var guid) || guid == Guid.Empty)
        {
            _logger.LogWarning("RegisterOrder rejected due to invalid correlationId. CorrelationId={CorrelationId} UserId={UserId}", correlationId, Context.UserIdentifier);
            throw new HubException("Invalid correlationId");
        }

        var corr = new CorrelationId(guid);
        try
        {
            await _correlationRegistry.RegisterAsync(corr, Context.UserIdentifier, Context.ConnectionAborted);
            _logger.LogInformation("Registered order correlation. CorrelationId={CorrelationId} UserId={UserId}", corr, Context.UserIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegisterOrder failed. CorrelationId={CorrelationId} UserId={UserId}", corr, Context.UserIdentifier);
            throw new HubException("RegisterOrder failed on server");
        }
    }

    /// <summary>
    /// Returns the current transient workflow status for the given correlation id, if present in Redis.
    /// </summary>
    public async Task<OrderWorkflowState?> GetCurrentStatus(string correlationId)
    {
        if (!Guid.TryParse(correlationId, out var guid) || guid == Guid.Empty)
        {
            _logger.LogWarning("GetCurrentStatus rejected due to invalid correlationId. CorrelationId={CorrelationId} UserId={UserId}", correlationId, Context.UserIdentifier);
            throw new HubException("Invalid correlationId");
        }

        var corr = new CorrelationId(guid);
        try
        {
            return await _workflowQuery.GetAsync(corr, Context.ConnectionAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCurrentStatus failed. CorrelationId={CorrelationId} UserId={UserId}", corr, Context.UserIdentifier);
            throw new HubException("GetCurrentStatus failed on server");
        }
    }
}
