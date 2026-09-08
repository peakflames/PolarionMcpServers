using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PolarionRemoteMcpServer.Authentication;
using PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Auth;

/// <summary>
/// Covers the substitutions an Okta *org* authorization server forces: a `cid` allowlist standing in
/// for `aud` binding, and no scope requirement. Every case here is reachable only through explicit
/// opt-in config — the default-shape cases at the bottom assert the untouched path.
/// </summary>
public class ClientIdBindingTests : IDisposable
{
    private const string AllowedClientId = "0oaTestClientAllowed";
    private const string OtherClientId = "0oaTestClientOther";
    private const string Alias = McpAuthTestConfigBuilder.DefaultAlias;

    private readonly McpAuthTestConfigBuilder _mcpAuth = new();
    private readonly List<IDisposable> _factories = [];

    public void Dispose()
    {
        foreach (var factory in _factories)
            factory.Dispose();

        _mcpAuth.Dispose();
    }

    private PolarionFakeFactory NewFactory()
    {
        var factory = new PolarionFakeFactory();
        factory.WithProject(Alias, "REALPROJ", isDefault: true);
        _factories.Add(factory);
        return factory;
    }

    // ---------------------------------------------------------------- cid allowlist

    [Fact]
    public async Task CidOnAllowlist_IsAccepted()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        var token = _mcpAuth.CreateAccessToken([], clientId: AllowedClientId);

        var response = await SendToolsListAsync(factory, token);

        Assert.True(response.IsSuccessStatusCode, $"expected success, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CidAbsentFromToken_IsForbidden_WhenAllowlistConfigured()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        // Authenticates fine — the signature, issuer and lifetime are all good. The denial is an
        // authorization decision, so it must be 403 and not 401: a 401 would tell the client to go
        // re-authenticate, which cannot possibly help.
        var token = _mcpAuth.CreateAccessToken([], clientId: null);

        var response = await SendToolsListAsync(factory, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CidNotOnAllowlist_IsForbidden()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        var token = _mcpAuth.CreateAccessToken([], clientId: OtherClientId);

        var response = await SendToolsListAsync(factory, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CidComparison_IsOrdinal_NotCaseInsensitive()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        var token = _mcpAuth.CreateAccessToken([], clientId: AllowedClientId.ToUpperInvariant());

        var response = await SendToolsListAsync(factory, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MultipleAllowedClientIds_EachIsAccepted()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId, OtherClientId);

        foreach (var clientId in new[] { AllowedClientId, OtherClientId })
        {
            var response = await SendToolsListAsync(factory, _mcpAuth.CreateAccessToken([], clientId: clientId));

            Assert.True(response.IsSuccessStatusCode, $"{clientId}: got {(int)response.StatusCode}");
        }
    }

    // ---------------------------------------------------------------- audience relaxation

    [Fact]
    public async Task ValidateAudienceFalse_AcceptsTokenWithForeignAudience()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        // An Okta org AS stamps `aud` with its own issuer, never the MCP canonical URI. This is the
        // token shape such a server actually produces.
        var token = _mcpAuth.CreateAccessToken([], clientId: AllowedClientId, audience: _mcpAuth.Issuer);

        var response = await SendToolsListAsync(factory, token);

        Assert.True(response.IsSuccessStatusCode, $"expected success, got {(int)response.StatusCode}");
    }

    [Fact]
    public void ValidateAudienceFalse_WithEmptyAllowlist_RefusesToStart()
    {
        var factory = NewFactory();
        _mcpAuth.Apply(factory).With("McpAuth:ValidateAudience", "false");

        var exception = Assert.ThrowsAny<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("AllowedClientIds", string.Join(" ", exception.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void BlankAllowedClientId_RefusesToStart()
    {
        var factory = NewFactory();
        _mcpAuth.Apply(factory)
            .With("McpAuth:ValidateAudience", "false")
            .With("McpAuth:AllowedClientIds:0", "   ");

        var exception = Assert.ThrowsAny<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("AllowedClientIds", string.Join(" ", exception.Failures), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- scope relaxation

    [Fact]
    public async Task RequireScopeFalse_AcceptsTokenWithNoScopes()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        var token = _mcpAuth.CreateAccessToken([], clientId: AllowedClientId);

        var response = await SendToolsListAsync(factory, token);

        Assert.True(response.IsSuccessStatusCode, $"expected success, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task RequireScopeFalse_AcceptsTokenWithOnlyOidcScopes()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        // The four scopes an org AS can actually grant. None of them is `polarion:read`.
        var token = _mcpAuth.CreateAccessToken(
            ["openid", "email", "profile", "offline_access"], clientId: AllowedClientId);

        var response = await SendToolsListAsync(factory, token);

        Assert.True(response.IsSuccessStatusCode, $"expected success, got {(int)response.StatusCode}");
    }

    // ---------------------------------------------------------------- defaults unchanged

    [Fact]
    public async Task DefaultShape_AcceptsTokenWithNoCidClaim()
    {
        var factory = NewFactory();
        _mcpAuth.Apply(factory);

        // Empty AllowedClientIds means "no client check". Safe only because
        // McpAuthOptionsValidator refuses the ValidateAudience=false pairing, proven above.
        var token = _mcpAuth.CreateAccessToken([ApiScopes.PolarionRead]);

        var response = await SendToolsListAsync(factory, token);

        Assert.True(response.IsSuccessStatusCode, $"expected success, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DefaultShape_StillRejectsForeignAudience()
    {
        var factory = NewFactory();
        _mcpAuth.Apply(factory);

        var token = _mcpAuth.CreateAccessToken(
            [ApiScopes.PolarionRead], audience: "https://someone-else.example.invalid/mcp");

        var response = await SendToolsListAsync(factory, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DefaultShape_StillRequiresTheReadScope()
    {
        var factory = NewFactory();
        _mcpAuth.Apply(factory);

        var token = _mcpAuth.CreateAccessToken(["openid", "email"]);

        var response = await SendToolsListAsync(factory, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------- advertised scopes

    [Fact]
    public async Task AdvertiseScopesFalse_PublishesAnEmptyScopesSupported()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId)
            .With("McpAuth:AdvertiseScopes", "false");

        var body = await GetResourceMetadataAsync(factory);

        Assert.Empty(body.GetProperty("scopes_supported").EnumerateArray());
    }

    [Fact]
    public async Task DefaultShape_StillAdvertisesTheReadScope()
    {
        var factory = NewFactory();
        _mcpAuth.Apply(factory);

        var body = await GetResourceMetadataAsync(factory);

        var scopes = body.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains(ApiScopes.PolarionRead, scopes);
    }

    [Fact]
    public async Task ConfiguredScopes_ReplaceTheDefault_RatherThanAppendingToIt()
    {
        var factory = NewFactory();
        _mcpAuth.ApplyOrgAuthorizationServerShape(factory, AllowedClientId);

        var scopes = await GetAdvertisedScopesAsync(factory);

        // The whole point. ConfigurationBinder appends to a non-empty collection, so without
        // ReplaceConfiguredScopesSupported this would be [polarion:read, openid, email, ...] and a
        // conforming client would request polarion:read from an authorization server that cannot
        // grant it — failing the authorization request outright with invalid_scope.
        Assert.Equal(McpAuthTestConfigBuilder.OrgAuthorizationServerScopes, scopes);
        Assert.DoesNotContain(ApiScopes.PolarionRead, scopes);
    }

    [Fact]
    public async Task ConfiguredScopes_DropBlankEntries()
    {
        var factory = NewFactory();
        _mcpAuth.Apply(factory)
            .With("McpAuth:ScopesSupported:0", "openid")
            .With("McpAuth:ScopesSupported:1", "   ")
            .With("McpAuth:ScopesSupported:2", "email");

        var scopes = await GetAdvertisedScopesAsync(factory);

        Assert.Equal(new[] { "openid", "email" }, scopes);
    }

    [Fact]
    public async Task SingleBlankConfiguredScope_AdvertisesNothing()
    {
        var factory = NewFactory();
        // The only way an environment variable can express an empty array. AdvertiseScopes=false
        // says the same thing more legibly, but both must land on the same published metadata.
        _mcpAuth.Apply(factory).With("McpAuth:ScopesSupported:0", "");

        var scopes = await GetAdvertisedScopesAsync(factory);

        Assert.Empty(scopes);
    }

    // ----------------------------------------------------------------

    private static async Task<string[]> GetAdvertisedScopesAsync(PolarionMcpServerFactory factory)
    {
        var body = await GetResourceMetadataAsync(factory);

        return body.GetProperty("scopes_supported")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();
    }

    private static async Task<JsonElement> GetResourceMetadataAsync(PolarionMcpServerFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/.well-known/oauth-protected-resource/{Alias}/mcp");

        Assert.True(response.IsSuccessStatusCode, $"got {(int)response.StatusCode}");

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static async Task<HttpResponseMessage> SendToolsListAsync(PolarionMcpServerFactory factory, string bearerToken)
    {
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{Alias}/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        return await client.SendAsync(request);
    }
}
