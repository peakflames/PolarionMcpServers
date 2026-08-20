using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PolarionRemoteMcpServer.Rbac.Caching;

/// <summary>
/// Decorates <see cref="IIdentityLookup"/> with a <see cref="TtlCache{TKey,TValue}"/> keyed on the
/// raw claim value — a decorator, not a change to <see cref="RbacIdentityFilter"/>, which keeps
/// depending on the plain <see cref="IIdentityResolver"/> shape.
///
/// Positive and ambiguous results use <c>Rbac:IdentityCacheTtlSeconds</c>; a not-found result uses a
/// fixed 30s negative TTL, deliberately a constant rather than a new option — without it, a caller
/// with a valid bearer token but no Polarion user costs one upstream call per tool call, and an
/// agentic client retrying a denial becomes an unbounded request amplifier against shared production
/// Polarion. 300s (the positive default) would fix that but deny a newly created user for five
/// minutes. <c>Ambiguous</c> is a config fact (more than one Polarion user matches the same claim
/// value), safe to cache at the positive TTL.
///
/// <see cref="StringComparer.Ordinal"/>, not <c>OrdinalIgnoreCase</c>: worst case is two cache
/// entries for <c>Bob@</c>/<c>bob@</c> (harmless, just a duplicate upstream call), versus aliasing two
/// distinct users onto one identity if Polarion's own matching turns out to be case-sensitive.
/// </summary>
internal sealed class CachingIdentityResolver : IIdentityResolver
{
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromSeconds(30);

    private readonly IIdentityLookup _inner;
    private readonly TtlCache<string, IdentityResult> _cache;
    private readonly TimeSpan _positiveTtl;
    private readonly ILogger<CachingIdentityResolver> _logger;

    public CachingIdentityResolver(
        IIdentityLookup inner,
        IOptions<RbacOptions> options,
        TimeProvider timeProvider,
        ILogger<CachingIdentityResolver> logger)
    {
        _inner = inner;
        _positiveTtl = TimeSpan.FromSeconds(options.Value.IdentityCacheTtlSeconds);
        _logger = logger;
        _cache = new TtlCache<string, IdentityResult>(
            options.Value.MaxCacheEntries, timeProvider, logger, "Rbac:IdentityCache", StringComparer.Ordinal);
    }

    /// <summary>Exposed so <c>AddRbac</c> can also register this instance under
    /// <see cref="IEvictableCache"/> for <see cref="RbacCacheJanitor"/>, without a second lookup.</summary>
    internal IEvictableCache Cache => _cache;

    public async Task<string?> ResolveAsync(string identityClaimValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cache.GetOrAddAsync(
                identityClaimValue,
                async ct =>
                {
                    var looked = await _inner.LookupAsync(identityClaimValue, ct);
                    var ttl = looked.Outcome == IdentityOutcome.NotFound ? NegativeTtl : _positiveTtl;
                    return (looked, ttl);
                },
                cancellationToken);

            return result.Outcome == IdentityOutcome.Resolved ? result.Username : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // IdentityLookupException (or anything else the factory threw) never gets cached — the
            // TtlCache removes the entry before rethrowing. Treat it the same way the uncached
            // resolver always has: fail closed to "unresolved", not "unrestricted".
            _logger.LogWarning(ex, "RBAC cached identity resolution failed for '{IdentityClaimValue}'.", identityClaimValue);
            return null;
        }
    }
}
