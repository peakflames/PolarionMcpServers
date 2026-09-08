using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using StubAuthorizationServer;
using PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

namespace PolarionRemoteMcpServer.Tests.Auth;

/// <summary>Asserts the resource server rejects every malformed bearer token shape, and accepts a
/// well-formed one.</summary>
public class ResourceServerTokenValidationTests : IDisposable
{
    private const string Alias = McpAuthTestConfigBuilder.DefaultAlias;

    private readonly McpAuthTestConfigBuilder _mcpAuth = new();
    private readonly PolarionFakeFactory _factory = new();

    public ResourceServerTokenValidationTests()
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
    public async Task ValidToken_IsAccepted()
    {
        var token = CreateToken(iss: _mcpAuth.Issuer, aud: McpAuthTestConfigBuilder.DefaultAudience);

        var response = await SendToolsListAsync(token);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task WrongAudience_IsRejected()
    {
        var token = CreateToken(iss: _mcpAuth.Issuer, aud: "https://someone-else.example.invalid/mcp");

        var response = await SendToolsListAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuer_IsRejected()
    {
        var token = CreateToken(iss: "https://not-the-configured-as.example.invalid", aud: McpAuthTestConfigBuilder.DefaultAudience);

        var response = await SendToolsListAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var token = CreateToken(
            iss: _mcpAuth.Issuer,
            aud: McpAuthTestConfigBuilder.DefaultAudience,
            issuedAt: DateTimeOffset.UtcNow.AddHours(-2),
            expiresAt: DateTimeOffset.UtcNow.AddHours(-1));

        var response = await SendToolsListAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnsignedToken_IsRejected()
    {
        var claims = BuildClaims(_mcpAuth.Issuer, McpAuthTestConfigBuilder.DefaultAudience, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));
        var token = Jwt.CreateUnsigned(claims);

        var response = await SendToolsListAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedByUnknownKey_IsRejected()
    {
        using var unknownKey = RSA.Create(2048);
        var claims = BuildClaims(_mcpAuth.Issuer, McpAuthTestConfigBuilder.DefaultAudience, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));

        // Signed with a key whose public half was never published to the stub AS's JWKS, but
        // claiming the real AS's key id — the resource server must resolve the key by kid from
        // JWKS, not trust the signature blindly.
        var token = Jwt.CreateSigned(claims, unknownKey, SigningKey.KeyId);

        var response = await SendToolsListAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Dictionary<string, object?> BuildClaims(
        string iss, string aud, DateTimeOffset issuedAt, DateTimeOffset expiresAt) => new()
    {
        ["iss"] = iss,
        ["aud"] = aud,
        ["sub"] = "test-subject",
        ["iat"] = Jwt.ToUnixTimeSeconds(issuedAt),
        ["exp"] = Jwt.ToUnixTimeSeconds(expiresAt),
        ["scope"] = "polarion:read",
    };

    private static string CreateToken(
        string iss, string aud, DateTimeOffset? issuedAt = null, DateTimeOffset? expiresAt = null)
    {
        var claims = BuildClaims(
            iss,
            aud,
            issuedAt ?? DateTimeOffset.UtcNow,
            expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5));

        return Jwt.CreateSigned(claims, SigningKey.Rsa, SigningKey.KeyId);
    }

    private async Task<HttpResponseMessage> SendToolsListAsync(string bearerToken)
    {
        var client = _factory.CreateClient();

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
