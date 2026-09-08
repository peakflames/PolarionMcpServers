namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// What <see cref="RbacIdentityFilter"/> resolves once per <c>tools/call</c> and makes available to
/// the rest of the request via <see cref="IRbacCallContextAccessor"/>. A new instance is set per
/// call, never reused across calls, so a reference captured mid-call can't observe a later call's
/// data racing in.
/// </summary>
public sealed class RbacCallContext
{
    public required string ToolName { get; init; }

    /// <summary>The Polarion project alias from the call's route (<c>/{alias}/mcp</c>). Null only if
    /// the route somehow carried none — every real MCP route mounts under an alias segment.</summary>
    public string? ProjectAlias { get; init; }

    /// <summary>Raw value of the configured <c>Rbac:IdentityClaim</c> from the caller's JWT. Null when
    /// the claim is absent.</summary>
    public string? IdentityClaimValue { get; init; }

    /// <summary>The Polarion username <see cref="IIdentityResolver"/> resolved
    /// <see cref="IdentityClaimValue"/> to. Null when resolution failed, was ambiguous, or was never
    /// attempted.</summary>
    public string? PolarionUsername { get; init; }
}
