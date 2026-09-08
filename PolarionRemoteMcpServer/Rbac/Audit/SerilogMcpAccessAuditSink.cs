using Microsoft.Extensions.Logging;

namespace PolarionRemoteMcpServer.Rbac.Audit;

/// <summary>
/// Written at Information, with a fixed EventId, structured properties only — never a token, PAT, or
/// `Authorization` header value. <see cref="AccessAuditRecord.OAuthSubject"/> and
/// <see cref="AccessAuditRecord.IdentityClaimValue"/> are opaque identifiers (a `sub` claim, an email
/// address) logged deliberately as part of this feature's stated purpose (attribution), not an
/// oversight of the "never log secrets" rule — that rule is about credentials and tokens, not about
/// who made a call.
/// </summary>
public sealed class SerilogMcpAccessAuditSink : IMcpAccessAuditSink
{
    private static readonly EventId AccessAuditEventId = new(90210, "McpAccessAudit");

    private readonly ILogger<SerilogMcpAccessAuditSink> _logger;

    public SerilogMcpAccessAuditSink(ILogger<SerilogMcpAccessAuditSink> logger)
    {
        _logger = logger;
    }

    public void Record(AccessAuditRecord record)
    {
        _logger.LogInformation(
            AccessAuditEventId,
            "MCP access audit: tool={ToolName} decision={Decision} blocked={Blocked} " +
            "decisionReason={DecisionReason} projectAlias={ProjectAlias} oauthSubject={OAuthSubject} " +
            "oauthClientId={OAuthClientId} jti={Jti} identityClaimValue={IdentityClaimValue} " +
            "polarionUsername={PolarionUsername} elapsedMs={ElapsedMilliseconds}",
            record.ToolName,
            record.Decision,
            record.Blocked,
            record.DecisionReason,
            record.ProjectAlias,
            record.OAuthSubject,
            record.OAuthClientId,
            record.Jti,
            record.IdentityClaimValue,
            record.PolarionUsername,
            record.ElapsedMilliseconds);
    }
}
