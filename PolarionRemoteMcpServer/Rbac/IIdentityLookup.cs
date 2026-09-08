namespace PolarionRemoteMcpServer.Rbac;

/// <summary>Distinguishes "definitively no such user" from "we could not ask" — collapsing these (as
/// <see cref="IIdentityResolver.ResolveAsync"/>'s bare <c>string?</c> does) would let a Polarion
/// outage get cached as a negative identity result.</summary>
internal enum IdentityOutcome
{
    Resolved,
    NotFound,
    Ambiguous,
}

internal readonly record struct IdentityResult(IdentityOutcome Outcome, string? Username);

/// <summary>
/// The cache-facing seam a concrete identity resolver also implements, alongside the public
/// <see cref="IIdentityResolver"/> callers already depend on. A definitive failure to ask (client
/// creation failure, non-success response, malformed data) throws rather than returning a value here
/// — <see cref="Caching.CachingIdentityResolver"/>'s cache factory relies on that to keep an upstream
/// error from ever being cached.
/// </summary>
internal interface IIdentityLookup
{
    Task<IdentityResult> LookupAsync(string claimValue, CancellationToken cancellationToken = default);
}

/// <summary>Thrown by <see cref="IIdentityLookup.LookupAsync"/> for every definitive "we could not
/// ask" failure — never for a real zero/ambiguous-match answer. <see cref="Reason"/> is a bounded,
/// log-safe cause id, never a response body.</summary>
internal sealed class IdentityLookupException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}
