using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OrderNotification.Worker.Auth;

/// <summary>
/// Development-only authentication.
///
/// The Bearer token value is treated as the logical user identifier.
/// This enforces that SignalR connections always have Context.UserIdentifier.
/// </summary>
public sealed class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var auth) || auth.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));

        var raw = auth.ToString();
        const string prefix = "Bearer ";

        if (!raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Authorization must be Bearer"));

        var userId = raw[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(AuthenticateResult.Fail("Bearer token is empty"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId)
        };

        var identity = new ClaimsIdentity(claims, DevAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DevAuthenticationDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
