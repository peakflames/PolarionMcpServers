using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PolarionRemoteMcpServer.Authentication;
using PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Auth;

public class ChallengeAndDiscoveryTests : IDisposable
{
    private const string Alias = McpAuthTestConfigBuilder.DefaultAlias;

    private readonly McpAuthTestConfigBuilder _mcpAuth = new();
    private readonly PolarionFakeFactory _factory = new();

    public ChallengeAndDiscoveryTests()
    {
        _mcpAuth.Apply(_factory);
        _factory.WithProject(Alias, "REALPROJ", isDefault: true);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _mcpAuth.Dispose();
    }

    [Fact]
    public async Task UnauthenticatedPost_Returns401WithResourceMetadataChallenge()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{Alias}/mcp")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Contains("resource_metadata", challenge.Parameter);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_DescribesThisAliasAndTheExternalAs()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/.well-known/oauth-protected-resource/{Alias}/mcp");

        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(McpAuthTestConfigBuilder.DefaultAudience, body.GetProperty("resource").GetString());

        var authServers = body.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains(_mcpAuth.Issuer, authServers);

        var scopes = body.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains(ApiScopes.PolarionRead, scopes);
    }

    [Fact]
    public async Task UnknownAlias_ProtectedResourceMetadata_Is404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource/nope/mcp");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LegacySseEndpoint_IsStillGoneWithAuthEnabled()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/sse");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
