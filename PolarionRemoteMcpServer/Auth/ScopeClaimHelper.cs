using System.Security.Claims;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// An AS-agnostic JWT may carry "scope" as a single space-delimited claim (RFC 9068 / RFC 6749
/// §3.3) or "scp" as a JSON array; the existing ApiKey scheme also emits one discrete "scope"
/// claim per scope. Splitting every claim value on spaces handles all three shapes with one code
/// path, so the resource-server gate isn't quietly bypassed by a claim-shape mismatch.
/// </summary>
public static class ScopeClaimHelper
{
    private const string ScopeClaimType = "scope";
    private const string ScpClaimType = "scp";

    public static bool HasScope(ClaimsPrincipal user, string requiredScope)
    {
        foreach (var scope in GetScopes(user))
        {
            if (string.Equals(scope, requiredScope, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static IEnumerable<string> GetScopes(ClaimsPrincipal user)
    {
        foreach (var claim in user.FindAll(ScopeClaimType))
        {
            foreach (var token in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                yield return token;
        }

        foreach (var claim in user.FindAll(ScpClaimType))
        {
            foreach (var token in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                yield return token;
        }
    }
}
