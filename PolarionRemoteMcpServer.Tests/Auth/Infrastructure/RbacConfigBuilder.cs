namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

public static class RbacConfigBuilder
{
    public static PolarionMcpServerFactory WithRbacEnabled(
        this PolarionMcpServerFactory factory, bool auditOnly = false, string identityClaim = "email")
    {
        return factory
            .With("Rbac:Enabled", "true")
            .With("Rbac:AuditOnly", auditOnly ? "true" : "false")
            .With("Rbac:IdentityClaim", identityClaim);
    }

    /// <summary>RBAC with identity resolved from the authorization server's <c>/userinfo</c> endpoint
    /// instead of a token claim — the Okta org-authorization-server shape.</summary>
    public static PolarionMcpServerFactory WithRbacUserInfoIdentity(
        this PolarionMcpServerFactory factory, bool auditOnly = false)
    {
        return factory
            .With("Rbac:Enabled", "true")
            .With("Rbac:AuditOnly", auditOnly ? "true" : "false")
            .With("Rbac:IdentitySource", "UserInfo");
    }
}
