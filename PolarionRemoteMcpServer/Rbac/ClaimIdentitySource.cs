using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// The pre-existing behavior, moved behind <see cref="IIdentitySource"/> unchanged: read
/// <c>Rbac:IdentityClaim</c> off the validated principal. No normalization and no guardrails are
/// applied here deliberately — doing so would silently change how existing deployments resolve
/// identity. The guardrails live in <see cref="OktaUserInfoEmailSource"/>, on the path that is new.
/// </summary>
internal sealed class ClaimIdentitySource : IIdentitySource
{
    private readonly IOptions<RbacOptions> _options;

    public ClaimIdentitySource(IOptions<RbacOptions> options)
    {
        _options = options;
    }

    public ValueTask<IdentitySourceResult> GetIdentityAsync(
        ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var value = user?.FindFirst(_options.Value.IdentityClaim)?.Value;

        return ValueTask.FromResult(value is null
            ? IdentitySourceResult.Unresolved(IdentityUnresolvedReasons.Unresolved)
            : IdentitySourceResult.Resolved(value));
    }
}
