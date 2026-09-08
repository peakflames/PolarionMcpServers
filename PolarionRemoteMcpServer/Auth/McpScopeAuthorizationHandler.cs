using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// Satisfies <see cref="McpScopeRequirement"/> when the caller holds the scope, or unconditionally
/// when <see cref="McpAuthOptions.RequireScope"/> is false.
///
/// Dropping the scope assertion is not a loss of authorization. An authorization server with no
/// custom-scope capability can only mint org-wide OIDC scopes, so a scope value there conveys
/// nothing resource-specific — it would be a string the resource server checks against itself. The
/// authorization decision that matters is made downstream by the RBAC project-membership gate
/// against Polarion's own data, which grants nothing on the strength of a scope claim.
/// </summary>
public sealed class McpScopeAuthorizationHandler : AuthorizationHandler<McpScopeRequirement>
{
    private readonly IOptions<McpAuthOptions> _authOptions;

    public McpScopeAuthorizationHandler(IOptions<McpAuthOptions> authOptions)
    {
        _authOptions = authOptions;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        McpScopeRequirement requirement)
    {
        if (!_authOptions.Value.RequireScope
            || ScopeClaimHelper.HasScope(context.User, requirement.RequiredScope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
