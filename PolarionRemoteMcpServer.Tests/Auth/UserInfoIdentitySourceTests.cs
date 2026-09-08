using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using PolarionRemoteMcpServer.Authentication;
using PolarionRemoteMcpServer.Rbac.Audit;
using PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Auth;

/// <summary>
/// Covers <c>Rbac:IdentitySource=UserInfo</c> — identity resolved by calling the authorization
/// server's OIDC <c>/userinfo</c> endpoint with the caller's own token, for an authorization server
/// that puts no <c>email</c> claim on an access token.
///
/// Every case asserts on the emitted <see cref="AccessAuditRecord"/> rather than on the HTTP status,
/// because the interesting distinctions (a throttled identity provider versus a caller who has no
/// identity) are invisible in the response by design: both are the same denial to the caller.
/// </summary>
public class UserInfoIdentitySourceTests : IDisposable
{
    private const string Email = "stub.user@example.invalid";
    private const string PolarionUsername = "stub-user";

    private readonly PolarionFakeFactory _factory = new();
    private readonly McpAuthTestConfigBuilder _mcpAuth = new();

    public UserInfoIdentitySourceTests()
    {
        _mcpAuth.Apply(_factory);
        _factory.WithProject(McpAuthTestConfigBuilder.DefaultAlias, "REALPROJ", isDefault: true);
        _factory.WithRbacUserInfoIdentity();
        _factory.IdentityLookup.Map(Email, PolarionUsername);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _mcpAuth.Dispose();
    }

    // ---------------------------------------------------------------- happy path

    [Fact]
    public async Task ResolvesIdentityFromUserInfo_WhenTokenCarriesNoEmailClaim()
    {
        // No `email` argument: the token deliberately carries no email claim at all, which is the
        // whole point — under IdentitySource=Claim this call would deny.
        var response = await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]));

        Assert.True(response.IsSuccessStatusCode);

        var record = Assert.Single(_factory.AuditSink.Records);
        Assert.Equal(Email, record.IdentityClaimValue);
        Assert.Equal(PolarionUsername, record.PolarionUsername);
        Assert.Equal(AccessDecision.Allow, record.Decision);
        Assert.False(record.Blocked);
        Assert.Equal(1, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task NormalizesEmailCase_BeforeThePolarionLookup()
    {
        _mcpAuth.State.UserInfoEmail = "Stub.User@Example.Invalid";

        // The fake only answers the lowercase locator, so a pass here proves normalization happened
        // before the lookup rather than after.
        var response = await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]));

        Assert.True(response.IsSuccessStatusCode);
        var record = Assert.Single(_factory.AuditSink.Records);
        Assert.Equal(Email, record.IdentityClaimValue);
        Assert.Equal(PolarionUsername, record.PolarionUsername);
    }

    // ---------------------------------------------------------------- guardrails

    [Fact]
    public async Task UnverifiedEmail_Denies_WithItsOwnReason()
    {
        _mcpAuth.State.UserInfoEmailVerified = false;

        await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]));

        var record = Assert.Single(_factory.AuditSink.Records);
        Assert.Null(record.IdentityClaimValue);
        Assert.Equal(AccessDecision.Deny, record.Decision);
        Assert.Equal("identity_email_unverified", record.DecisionReason);
        Assert.True(record.Blocked);
    }

    [Fact]
    public async Task PlusAddressedEmail_Denies_RatherThanNormalizingToTheBaseAddress()
    {
        _mcpAuth.State.UserInfoEmail = "stub.user+polarion@example.invalid";

        // Deliberately registered so the test would PASS the lookup if the code stripped the tag —
        // the assertion is that it does not even try.
        await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]));

        var record = Assert.Single(_factory.AuditSink.Records);
        Assert.Null(record.IdentityClaimValue);
        Assert.Equal("identity_email_rejected_form", record.DecisionReason);
        Assert.True(record.Blocked);
    }

    [Fact]
    public async Task NoEmailAtTheIdentityProvider_Denies_WithTheGenericUnresolvedReason()
    {
        _mcpAuth.State.UserInfoEmail = null;

        await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]));

        var record = Assert.Single(_factory.AuditSink.Records);
        Assert.Null(record.IdentityClaimValue);
        Assert.Equal("identity_unresolved", record.DecisionReason);
        Assert.True(record.Blocked);
    }

    // ---------------------------------------------------------------- 429 handling

    [Fact]
    public async Task RateLimited_Denies_WithADistinctReason_AndIsNotCached()
    {
        _mcpAuth.State.UserInfoRateLimited = true;

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]);

        await CallListSpacesAsync(token);
        await CallListSpacesAsync(token);

        Assert.Equal(2, _factory.AuditSink.Records.Count);
        foreach (var record in _factory.AuditSink.Records)
        {
            Assert.Equal("identity_userinfo_rate_limited", record.DecisionReason);
            Assert.True(record.Blocked);
        }

        // The point of the test: a 429 must not be cached, even negatively. Two calls on the same
        // token therefore produce two upstream attempts, not one cached denial reused.
        Assert.Equal(2, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task RateLimitClearing_RecoversOnTheVeryNextCall()
    {
        _mcpAuth.State.UserInfoRateLimited = true;

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]);
        await CallListSpacesAsync(token);

        _mcpAuth.State.UserInfoRateLimited = false;
        var response = await CallListSpacesAsync(token);

        // Would fail if the 429 had been cached: the second call would still be serving the denial.
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, _factory.AuditSink.Records.Count);
        Assert.Equal("identity_userinfo_rate_limited", _factory.AuditSink.Records[0].DecisionReason);
        Assert.Equal(Email, _factory.AuditSink.Records[1].IdentityClaimValue);
    }

    [Fact]
    public async Task UpstreamServerError_Denies_WithTheUnavailableReason_AndIsNotCached()
    {
        _mcpAuth.State.UserInfoStatusCode = StatusCodes.Status503ServiceUnavailable;

        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]);
        await CallListSpacesAsync(token);
        await CallListSpacesAsync(token);

        Assert.All(_factory.AuditSink.Records, r => Assert.Equal("identity_userinfo_unavailable", r.DecisionReason));
        Assert.Equal(2, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task MalformedUserInfoResponse_Denies_WithTheUnavailableReason()
    {
        _mcpAuth.State.UserInfoMalformed = true;

        await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]));

        var record = Assert.Single(_factory.AuditSink.Records);
        Assert.Equal("identity_userinfo_unavailable", record.DecisionReason);
    }

    // ---------------------------------------------------------------- caching

    [Fact]
    public async Task ABurstOfCallsOnOneToken_CostsExactlyOneUserInfoRequest()
    {
        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]);

        for (var i = 0; i < 8; i++)
        {
            var response = await CallListSpacesAsync(token);
            Assert.True(response.IsSuccessStatusCode, $"call {i} failed");
        }

        Assert.Equal(8, _factory.AuditSink.Records.Count);
        Assert.Equal(1, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task ADifferentToken_CostsOneMoreUserInfoRequest()
    {
        // Distinct subjects so the two tokens differ byte-wise, which is what the SHA-256 cache key
        // is computed over. One /userinfo call per fresh token.
        await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], subject: "subject-one"));
        await CallListSpacesAsync(_mcpAuth.CreateAccessToken([ApiScopes.PolarionRead], subject: "subject-two"));

        Assert.Equal(2, _mcpAuth.State.UserInfoRequestCount);
    }

    [Fact]
    public async Task ConcurrentCallsOnOneToken_ShareASingleUserInfoRequest()
    {
        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CallListSpacesAsync(token)));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));

        // TtlCache's single-flight coalescing, exercised for real: a cache that merely stored results
        // would issue six requests here.
        Assert.Equal(1, _mcpAuth.State.UserInfoRequestCount);
    }

    // ----------------------------------------------------------------

    private async Task<HttpResponseMessage> CallListSpacesAsync(string bearerToken)
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{McpAuthTestConfigBuilder.DefaultAlias}/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_spaces","arguments":{}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        return await client.SendAsync(request);
    }
}
