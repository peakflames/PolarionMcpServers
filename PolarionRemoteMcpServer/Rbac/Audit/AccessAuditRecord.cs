namespace PolarionRemoteMcpServer.Rbac.Audit;

public enum AccessDecision
{
    Allow,
    Deny,
}

/// <summary>
/// One record per <c>tools/call</c> made while RBAC is enabled — the artifact a security reviewer
/// checks for "who did what" in place of Polarion's own audit trail, which can only ever name the
/// shared service account.
///
/// <see cref="Decision"/> and <see cref="DecisionReason"/> always carry the gate's true verdict,
/// regardless of <c>Rbac:AuditOnly</c> — <see cref="Blocked"/> is the separate field recording
/// whether the call was actually stopped. Without this split, AuditOnly shadow mode would log every
/// would-be deny as an Allow, producing none of the data it exists to produce.
/// </summary>
public sealed record AccessAuditRecord(
    DateTimeOffset Timestamp,
    string? OAuthSubject,
    string? OAuthClientId,
    string? Jti,
    string? IdentityClaimValue,
    string? PolarionUsername,
    string ToolName,
    string? ProjectAlias,
    AccessDecision Decision,
    string? DecisionReason,
    bool Blocked,
    long ElapsedMilliseconds);
