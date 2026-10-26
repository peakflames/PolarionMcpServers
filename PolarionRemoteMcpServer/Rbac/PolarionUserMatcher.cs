namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// Picks the Polarion user for an email claim value. Separate from <see cref="PolarionIdentityLookup"/>
/// so each rule is a unit test rather than a Polarion round trip.
///
/// The candidates are the users whose <c>email</c> equals the claim value (<c>OrdinalIgnoreCase</c>).
/// One candidate resolves to that user; none is <see cref="IdentityOutcome.NotFound"/>. With several,
/// the one whose <c>id</c> equals the email's local part wins, if exactly one does; otherwise the
/// result is <see cref="IdentityOutcome.Ambiguous"/>.
///
/// The tiebreak exists because sharing an email is legitimate: a person owns service accounts under
/// their own address, so the same email appears on <c>alice</c> and <c>alice_toolkit_access</c>. It
/// chooses only among email matches — a user whose email does not match is never selected, whatever
/// their id — and is skipped unless the claim value has exactly one <c>@</c> and a non-empty local
/// part, since <see cref="ClaimIdentitySource"/> applies no guardrails. Sharing an email is expected,
/// so the tiebreak firing is not logged.
/// </summary>
internal static class PolarionUserMatcher
{
    internal static IdentityResult Match(IEnumerable<(string? Id, string? Email)> users, string claimValue)
    {
        var matches = users
            .Where(u => !string.IsNullOrEmpty(u.Id)
                && !string.IsNullOrEmpty(u.Email)
                && string.Equals(u.Email, claimValue, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            return new IdentityResult(IdentityOutcome.NotFound, null);
        }

        if (matches.Count == 1)
        {
            return new IdentityResult(IdentityOutcome.Resolved, matches[0].Id);
        }

        var localPart = LocalPart(claimValue);
        if (localPart is not null)
        {
            var byId = matches
                .Where(u => string.Equals(u.Id, localPart, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (byId.Count == 1)
            {
                return new IdentityResult(IdentityOutcome.Resolved, byId[0].Id);
            }
        }

        return new IdentityResult(IdentityOutcome.Ambiguous, null);
    }

    /// <summary>The part before the single <c>@</c>, or null if there is not exactly one <c>@</c> or
    /// the local part is empty.</summary>
    private static string? LocalPart(string claimValue)
    {
        var at = claimValue.IndexOf('@');
        if (at <= 0 || at != claimValue.LastIndexOf('@'))
        {
            return null;
        }

        return claimValue[..at];
    }
}
