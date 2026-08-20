using Microsoft.AspNetCore.Authorization;
using PolarionRemoteMcpServer.Auth;
using Serilog;

namespace PolarionRemoteMcpServer.Authentication;

/// <summary>
/// Authorization requirement that checks for a specific scope claim.
/// </summary>
public class ScopeRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The required scope.
    /// </summary>
    public string Scope { get; }

    public ScopeRequirement(string scope)
    {
        Scope = scope;
    }
}

/// <summary>
/// Authorization handler that validates scope claims.
/// </summary>
public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        // Routed through ScopeClaimHelper rather than a raw FindAll("scope") check so this
        // handler also works if it is ever reached by a JWT principal, whose scopes may arrive as
        // a single space-delimited "scope" claim or as discrete "scp" claims instead of the
        // ApiKey scheme's one-claim-per-scope shape.
        if (ScopeClaimHelper.HasScope(context.User, requirement.Scope))
        {
            Log.Debug("Authorization: User has required scope '{Scope}'", requirement.Scope);
            context.Succeed(requirement);
        }
        else
        {
            var consumerId = context.User.FindFirst("consumer_id")?.Value ?? "unknown";
            var scopeClaims = ScopeClaimHelper.GetScopes(context.User).ToList();
            Log.Warning("Authorization: Consumer '{ConsumerId}' missing required scope '{Scope}'. Has scopes: [{Scopes}]",
                consumerId, requirement.Scope, string.Join(", ", scopeClaims));
        }

        return Task.CompletedTask;
    }
}
