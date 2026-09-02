using Microsoft.Extensions.Options;
using PolarionRemoteMcpServer.Auth;

namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// Returns Success immediately when Rbac is disabled, same shape as <c>McpAuthOptionsValidator</c>,
/// so a malformed Rbac section can never break a server with RBAC off.
///
/// The one rule that matters most: RBAC without authentication means no identity for every caller,
/// which means every gated call fails closed — a 100% denial rate that presents as an outage, not a
/// misconfiguration. Refusing to boot here turns that outage into an actionable startup error.
/// </summary>
public sealed class RbacOptionsValidator : IValidateOptions<RbacOptions>
{
    private readonly IOptions<McpAuthOptions> _mcpAuthOptions;

    public RbacOptionsValidator(IOptions<McpAuthOptions> mcpAuthOptions)
    {
        _mcpAuthOptions = mcpAuthOptions;
    }

    public ValidateOptionsResult Validate(string? name, RbacOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (!_mcpAuthOptions.Value.Enabled)
        {
            failures.Add(
                "Rbac:Enabled requires McpAuth:Enabled — without authentication there is no caller " +
                "identity to check, so every gated call would fail closed for every caller. Enable " +
                "McpAuth first, or leave Rbac disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.IdentityClaim))
            failures.Add("Rbac:IdentityClaim must not be blank.");

        if (options.IdentityCacheTtlSeconds < 1 || options.IdentityCacheTtlSeconds > 86400)
            failures.Add("Rbac:IdentityCacheTtlSeconds must be between 1 and 86400.");

        if (options.IdentitySource == IdentitySource.UserInfo && string.IsNullOrWhiteSpace(_mcpAuthOptions.Value.Issuer))
        {
            failures.Add(
                "Rbac:IdentitySource=UserInfo requires McpAuth:Issuer — the /userinfo endpoint is " +
                "derived from it.");
        }

        if (options.UserInfoCacheTtlSeconds < 1 || options.UserInfoCacheTtlSeconds > 86400)
            failures.Add("Rbac:UserInfoCacheTtlSeconds must be between 1 and 86400.");

        if (options.MembershipCacheTtlSeconds < 1 || options.MembershipCacheTtlSeconds > 86400)
            failures.Add("Rbac:MembershipCacheTtlSeconds must be between 1 and 86400.");

        if (options.MaxCacheEntries < 1 || options.MaxCacheEntries > 10_000_000)
            failures.Add("Rbac:MaxCacheEntries must be between 1 and 10000000.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
