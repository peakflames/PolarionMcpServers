using PolarionRemoteMcpServer.Rbac;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Stands in for <see cref="PolarionIdentityLookup"/> — a fixed, test-authored email-to-username
/// table instead of a case-insensitive SOAP lookup against Polarion's own user list. Defaults to
/// <see cref="IdentityOutcome.NotFound"/> for anything not explicitly mapped, matching the real
/// lookup's fail-closed default for an unrecognized caller.
/// </summary>
internal sealed class FakeIdentityLookup : IIdentityLookup
{
    private readonly Dictionary<string, string> _byEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _throwing = new(StringComparer.OrdinalIgnoreCase);

    public FakeIdentityLookup Map(string email, string username)
    {
        _byEmail[email] = username;
        return this;
    }

    /// <summary>The next lookup for this email throws <see cref="IdentityLookupException"/> instead
    /// of returning a definitive answer — the "we could not ask" path, distinct from a genuine
    /// zero-match NotFound.</summary>
    public FakeIdentityLookup ThrowFor(string email)
    {
        _throwing.Add(email);
        return this;
    }

    public Task<IdentityResult> LookupAsync(string claimValue, CancellationToken cancellationToken = default)
    {
        if (_throwing.Contains(claimValue))
            throw new IdentityLookupException("stub_lookup_failure");

        return Task.FromResult(_byEmail.TryGetValue(claimValue, out var username)
            ? new IdentityResult(IdentityOutcome.Resolved, username)
            : new IdentityResult(IdentityOutcome.NotFound, null));
    }
}
