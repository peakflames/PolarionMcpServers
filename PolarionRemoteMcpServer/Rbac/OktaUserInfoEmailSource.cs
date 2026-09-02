using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarionRemoteMcpServer.Auth;
using PolarionRemoteMcpServer.Rbac.Caching;
using PolarionRemoteMcpServer.Rbac.Models;

namespace PolarionRemoteMcpServer.Rbac;

/// <summary>
/// Resolves the caller's email from the authorization server's OIDC <c>/userinfo</c> endpoint, using
/// the caller's own bearer token. Required for an Okta *org* authorization server, which puts no
/// <c>email</c> claim on an access token at any price.
///
/// No new server-held secret: the request is made with the token the caller already presented, so
/// this server gains no standing credential against the identity provider and can only ever read the
/// profile of whoever is calling it.
///
/// Downstream is untouched — the resolved email flows into the existing
/// <see cref="PolarionIdentityLookup"/> (case-insensitive email join) exactly as a claim value would.
///
/// Cache key is a SHA-256 of the raw token, never the token. Cache TTL is
/// <c>min(remaining token lifetime, Rbac:UserInfoCacheTtlSeconds)</c> so an entry can never outlive
/// the credential it was fetched with. A transient failure — 429 above all — is thrown from the cache
/// factory rather than returned, because <see cref="TtlCache{TKey,TValue}"/> removes an entry whose
/// factory threw before rethrowing: that is the mechanism keeping a throttling event out of the cache.
/// Caching a 429 negatively would convert one throttled second into a five-minute outage for that user.
/// </summary>
internal sealed class OktaUserInfoEmailSource : IIdentitySource
{
    internal const string HttpClientName = "OidcUserInfo";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<McpAuthOptions> _authOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OktaUserInfoEmailSource> _logger;
    private readonly TtlCache<string, IdentitySourceResult> _cache;
    private readonly TimeSpan _maxTtl;

    public OktaUserInfoEmailSource(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IOptions<McpAuthOptions> authOptions,
        IOptions<RbacOptions> rbacOptions,
        TimeProvider timeProvider,
        ILogger<OktaUserInfoEmailSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _authOptions = authOptions;
        _timeProvider = timeProvider;
        _logger = logger;
        _maxTtl = TimeSpan.FromSeconds(rbacOptions.Value.UserInfoCacheTtlSeconds);
        _cache = new TtlCache<string, IdentitySourceResult>(
            rbacOptions.Value.MaxCacheEntries, timeProvider, logger, "Rbac:UserInfoCache", StringComparer.Ordinal);
    }

    /// <summary>Exposed so <c>AddRbac</c> can register this instance under <c>IEvictableCache</c> for
    /// <see cref="RbacCacheJanitor"/> without a second lookup — same shape as
    /// <see cref="CachingIdentityResolver.Cache"/>.</summary>
    internal IEvictableCache Cache => _cache;

    public async ValueTask<IdentitySourceResult> GetIdentityAsync(
        ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var token = _httpContextAccessor.HttpContext?.Items[McpAuthHttpContextItems.RawAccessToken] as string;
        if (string.IsNullOrEmpty(token))
        {
            // Reachable only if authentication is off or the capture hook did not run — either way
            // there is nothing to call /userinfo with, and fail-closed is the answer.
            _logger.LogWarning(
                "RBAC identity source is UserInfo but no validated bearer token was captured for this request.");
            return IdentitySourceResult.Unresolved(IdentityUnresolvedReasons.NoBearerToken);
        }

        var cacheKey = HashToken(token);

        try
        {
            return await _cache.GetOrAddAsync(
                cacheKey,
                async ct => (await FetchAsync(token, ct), EffectiveTtl(user)),
                cancellationToken);
        }
        catch (UserInfoUnavailableException ex)
        {
            return IdentitySourceResult.Unresolved(ex.Reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RBAC /userinfo identity resolution failed.");
            return IdentitySourceResult.Unresolved(IdentityUnresolvedReasons.UserInfoUnavailable);
        }
    }

    /// <summary>
    /// An entry must not outlive the token it was fetched with — otherwise a token revoked one minute
    /// after issue would keep resolving to a live identity for the rest of the cache TTL. `exp` comes
    /// from the already-validated principal, so it is not attacker-controlled here.
    /// </summary>
    private TimeSpan EffectiveTtl(ClaimsPrincipal? user)
    {
        var expClaim = user?.FindFirst("exp")?.Value;
        if (!long.TryParse(expClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp))
            return _maxTtl;

        var remaining = DateTimeOffset.FromUnixTimeSeconds(exp) - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return remaining < _maxTtl ? remaining : _maxTtl;
    }

    private async Task<IdentitySourceResult> FetchAsync(string token, CancellationToken cancellationToken)
    {
        var endpoint = UserInfoEndpoint();
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "RBAC /userinfo transport failure.");
            throw new UserInfoUnavailableException(IdentityUnresolvedReasons.UserInfoUnavailable);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Logged at Error, not Warning, and with its own distinct reason id: this endpoint sits
                // in an org-wide Okta rate-limit bucket shared with every other application in the
                // same Okta org, so being throttled here is a cross-application operational event
                // that must be alertable and must never look like "this user has no identity".
                _logger.LogError(
                    "RBAC /userinfo was rate limited by the authorization server (HTTP 429). Retry-After: " +
                    "{RetryAfter}. This is an org-wide quota shared with other applications — every RBAC " +
                    "call is denying while it persists.",
                    response.Headers.RetryAfter?.ToString() ?? "(absent)");

                throw new UserInfoUnavailableException(IdentityUnresolvedReasons.UserInfoRateLimited);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "RBAC /userinfo returned HTTP {StatusCode}.", (int)response.StatusCode);
                throw new UserInfoUnavailableException(IdentityUnresolvedReasons.UserInfoUnavailable);
            }

            OidcUserInfoResponse? payload;
            try
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                payload = JsonSerializer.Deserialize(json, RbacJsonContext.Default.OidcUserInfoResponse);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "RBAC /userinfo returned a malformed response.");
                throw new UserInfoUnavailableException(IdentityUnresolvedReasons.UserInfoUnavailable);
            }

            if (payload?.Email is null)
            {
                // A definitive answer, not a failure: this token's subject has no email at the IdP, so
                // it is safe (and desirable) to cache. Nothing about it is retryable.
                _logger.LogWarning("RBAC /userinfo returned no email for the calling subject.");
                return IdentitySourceResult.Unresolved(IdentityUnresolvedReasons.Unresolved);
            }

            if (payload.EmailVerified is not true)
            {
                _logger.LogWarning(
                    "RBAC /userinfo returned an unverified email for the calling subject — denying. " +
                    "An unverified address is an unauthenticated assertion.");
                return IdentitySourceResult.Unresolved(IdentityUnresolvedReasons.EmailUnverified);
            }

            if (!EmailIdentityGuardrails.TryNormalize(payload.Email, out var normalized, out var reason))
            {
                _logger.LogWarning(
                    "RBAC /userinfo email failed the identity guardrails ({Reason}) — denying rather than " +
                    "normalizing an alias form the identity provider has not mapped.",
                    reason);
                return IdentitySourceResult.Unresolved(reason ?? IdentityUnresolvedReasons.EmailRejectedForm);
            }

            return IdentitySourceResult.Resolved(normalized);
        }
    }

    /// <summary>
    /// Built from <c>McpAuth:Issuer</c> rather than read from the discovery document. The issuer is
    /// already the trust anchor for token validation, so deriving one more path from it introduces no
    /// new trust; fetching discovery here would add a second network dependency on the request path
    /// for a URL that is fixed for the life of the deployment.
    /// </summary>
    private string UserInfoEndpoint() =>
        $"{_authOptions.Value.Issuer.TrimEnd('/')}/oauth2/v1/userinfo";

    /// <summary>SHA-256 hex of the raw token. A cache keyed on the token itself would put a live
    /// credential in a long-lived dictionary and, worse, in any future dump of that dictionary.</summary>
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Signals "we could not ask" so the <see cref="TtlCache{TKey,TValue}"/> factory
    /// discards the entry. Mirrors <see cref="PolarionIdentityLookup"/>'s failure signaling role on
    /// the Polarion lookup path; <see cref="Reason"/> is a bounded reason id, never a response body.</summary>
    private sealed class UserInfoUnavailableException(string reason) : Exception(reason)
    {
        public string Reason { get; } = reason;
    }
}
