using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// Resolves <see cref="McpAuthOptions"/> from DI at evaluation time rather than reading config into
/// the policy lambda at registration time — the same reasoning as
/// <see cref="ConfigureJwtBearerOptions"/>. A policy lambda cannot see the service provider, so an
/// eagerly-captured allowlist would also make ValidateOnStart decorative.
///
/// An empty allowlist means "no client check", not "deny everything". That is safe only because
/// <see cref="McpAuthOptionsValidator"/> refuses to start the app when the allowlist is empty *and*
/// <see cref="McpAuthOptions.ValidateAudience"/> is false, so the dangerous pair — no audience
/// binding and no client binding — is unreachable at runtime. Once a non-empty allowlist is
/// configured, this handler fails closed: a missing `cid` claim is a denial, not a pass.
/// </summary>
public sealed class ClientIdAuthorizationHandler : AuthorizationHandler<ClientIdRequirement>
{
    private readonly IOptions<McpAuthOptions> _authOptions;

    public ClientIdAuthorizationHandler(IOptions<McpAuthOptions> authOptions)
    {
        _authOptions = authOptions;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ClientIdRequirement requirement)
    {
        var allowed = _authOptions.Value.AllowedClientIds;

        if (allowed is null || allowed.Count == 0)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var clientId = context.User.FindFirst(ClientIdRequirement.ClaimType)?.Value;
        if (string.IsNullOrEmpty(clientId))
        {
            // No Fail() call: leaving the requirement unmet is enough to deny, and Fail() would
            // also suppress any other handler that could legitimately satisfy it.
            return Task.CompletedTask;
        }

        foreach (var candidate in allowed)
        {
            if (string.Equals(candidate, clientId, StringComparison.Ordinal))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}
