using Microsoft.AspNetCore.Authorization;
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
        // Get all scope claims
        var scopeClaims = context.User.FindAll("scope").Select(c => c.Value).ToList();

        if (scopeClaims.Contains(requirement.Scope))
        {
            Log.Debug("Authorization: User has required scope '{Scope}'", requirement.Scope);
            context.Succeed(requirement);
        }
        else
        {
            var consumerId = context.User.FindFirst("consumer_id")?.Value ?? "unknown";
            Log.Warning("Authorization: Consumer '{ConsumerId}' missing required scope '{Scope}'. Has scopes: [{Scopes}]",
                consumerId, requirement.Scope, string.Join(", ", scopeClaims));
        }

        return Task.CompletedTask;
    }
}
