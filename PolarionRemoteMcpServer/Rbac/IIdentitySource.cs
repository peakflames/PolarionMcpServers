using System.Security.Claims;

namespace PolarionRemoteMcpServer.Rbac;

public enum IdentitySource
{
    /// <summary>Read the value straight off the validated access token. Current, default behavior.</summary>
    Claim,

    /// <summary>Call the authorization server's OIDC <c>/userinfo</c> endpoint with the caller's own
    /// access token. For an authorization server that cannot put the claim on the token.</summary>
    UserInfo,
}

/// <summary>
/// Where the identity value comes from, behind one seam so <see cref="RbacIdentityFilter"/> does not
/// grow a branch. <see cref="IdentitySourceResult"/>'s <c>UnresolvedReason</c> exists so a
/// rate-limited authorization server is distinguishable in the audit log from a caller who genuinely
/// has no identity — collapsing the two would make an outage look exactly like a routine denial.
/// </summary>
internal readonly record struct IdentitySourceResult(string? Value, string? UnresolvedReason)
{
    internal static IdentitySourceResult Resolved(string value) => new(value, null);

    internal static IdentitySourceResult Unresolved(string reason) => new(null, reason);
}

internal interface IIdentitySource
{
    ValueTask<IdentitySourceResult> GetIdentityAsync(ClaimsPrincipal? user, CancellationToken cancellationToken);
}

/// <summary>Bounded, log-safe reason ids. Never a response body, never the email itself.</summary>
internal static class IdentityUnresolvedReasons
{
    /// <summary>The claim (or the /userinfo email field) simply isn't there. The pre-existing
    /// reason string — kept byte-identical so existing audit queries and tests still match.</summary>
    internal const string Unresolved = "identity_unresolved";

    internal const string NoBearerToken = "identity_no_bearer_token";
    internal const string EmailUnverified = "identity_email_unverified";
    internal const string EmailRejectedForm = "identity_email_rejected_form";

    /// <summary>The alertable one. The authorization server is throttling us, which is an operational
    /// event, not a statement about this caller.</summary>
    internal const string UserInfoRateLimited = "identity_userinfo_rate_limited";

    internal const string UserInfoUnavailable = "identity_userinfo_unavailable";
}
