using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace OrderNotification.Worker.Auth;

/// <summary>
/// Resolves SignalR UserIdentifier from standard claim types.
/// </summary>
public sealed class ClaimsUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var user = connection.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
    }
}
