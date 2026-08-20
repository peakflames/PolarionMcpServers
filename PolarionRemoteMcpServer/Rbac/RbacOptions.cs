namespace PolarionRemoteMcpServer.Rbac;

// Deliberately `sealed class`, never `record` — same rationale as `McpAuthOptions`: a record's
// generated ToString() would print every property, and this section sits next to config that is
// sensitive even though nothing here is secret today.

/// <summary>
/// <c>FailClosed</c> is intentionally not a property here: it is always true and is not
/// configurable. An unresolved identity or an upstream error both deny — there is no knob to
/// relax that.
/// </summary>
public sealed class RbacOptions
{
    public const string SectionName = "Rbac";

    public bool Enabled { get; set; }

    /// <summary>The rollout lever: when true, every check still runs and is still audited, but a
    /// DENY decision never blocks the call. Lets the gate mechanism ship dark before it is trusted to
    /// actually deny anyone.</summary>
    public bool AuditOnly { get; set; }

    /// <summary>Which JWT claim resolves to a Polarion identity (by default, the caller's email
    /// address, joined against the Polarion user's own email field).</summary>
    public string IdentityClaim { get; set; } = "email";

    public int IdentityCacheTtlSeconds { get; set; } = 300;

    /// <summary>TTL for <see cref="PolarionProjectUsersGate"/>'s per-project membership cache.</summary>
    public int MembershipCacheTtlSeconds { get; set; } = 120;

    /// <summary>Saturation cap for every RBAC cache, mirroring <c>McpAuth:MaxPendingAuthorizationCodes</c>'s
    /// role as a deliberate bound rather than an unbounded dictionary.</summary>
    public int MaxCacheEntries { get; set; } = 20000;
}
