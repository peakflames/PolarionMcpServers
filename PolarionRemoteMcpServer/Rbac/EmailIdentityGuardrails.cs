namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// The identity guardrails that apply to an email address *before* it becomes a lookup key.
/// Separate from the resolver so each rule is a unit test rather than an HTTP round trip.
///
/// Not applied on the <see cref="IdentitySource.Claim"/> path — see
/// <see cref="ClaimIdentitySource"/> for why.
/// </summary>
internal static class EmailIdentityGuardrails
{
    /// <summary>
    /// Returns the normalized address, or null with a reason id.
    ///
    /// Plus-addressing and other alias forms are rejected rather than normalized. Stripping
    /// <c>+tag</c> to reach the base address is a mapping decision the identity provider has not
    /// made: if the IdP considers <c>a+b@x</c> a distinct principal and we resolve it to <c>a@x</c>'s
    /// Polarion user, we have granted one person another person's authority on a guess. Denying is
    /// the only answer that cannot do that.
    /// </summary>
    internal static bool TryNormalize(string? email, out string normalized, out string? rejectionReason)
    {
        normalized = string.Empty;
        rejectionReason = null;

        if (string.IsNullOrWhiteSpace(email))
        {
            rejectionReason = IdentityUnresolvedReasons.Unresolved;
            return false;
        }

        var trimmed = email.Trim();

        // Exactly one '@', with something on both sides. Anything else is not an address we are
        // willing to guess about.
        var at = trimmed.IndexOf('@');
        if (at <= 0
            || at != trimmed.LastIndexOf('@')
            || at == trimmed.Length - 1)
        {
            rejectionReason = IdentityUnresolvedReasons.EmailRejectedForm;
            return false;
        }

        var localPart = trimmed[..at];
        if (localPart.Contains('+', StringComparison.Ordinal))
        {
            rejectionReason = IdentityUnresolvedReasons.EmailRejectedForm;
            return false;
        }

        // No whitespace anywhere, and no comment/quoting syntax — an address carrying any of these
        // is either malformed or deliberately obfuscated, and either way must not become a locator.
        foreach (var c in trimmed)
        {
            if (char.IsWhiteSpace(c) || c is '"' or '(' or ')' or ',' or ';' or '<' or '>' or '\\')
            {
                rejectionReason = IdentityUnresolvedReasons.EmailRejectedForm;
                return false;
            }
        }

        // Lowercased so one person cannot occupy two cache entries or two audit identities. Invariant,
        // not current-culture: the process runs with InvariantGlobalization=true, and a culture-sensitive
        // fold would make identity resolution depend on the host's locale.
        normalized = trimmed.ToLowerInvariant();
        return true;
    }
}
