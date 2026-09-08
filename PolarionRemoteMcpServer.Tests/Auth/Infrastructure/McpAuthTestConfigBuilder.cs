using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StubAuthorizationServer;

namespace PolarionRemoteMcpServer.Tests.Auth.Infrastructure;

/// <summary>
/// Starts a real stub external AS on a loopback Kestrel listener and wires a
/// <see cref="PolarionMcpServerFactory"/>'s <c>McpAuth:*</c> config to point at it.
/// <c>ConfigureJwtBearerOptions</c> fetches JWKS from <c>McpAuth:Issuer</c> over real HTTP, so tests
/// need a real (if minimal) AS listening somewhere, not just a shared key.
/// </summary>
public sealed class McpAuthTestConfigBuilder : IDisposable
{
    /// <summary>Base URL only — no alias, no trailing /mcp. See McpAuthOptions.ResourceUri /
    /// ResourceFor.</summary>
    public const string ResourceUri = "https://polarion-mcp.example.invalid";

    /// <summary>The alias every test in this suite defaults to registering via
    /// <see cref="PolarionMcpServerFactory.WithProject"/>.</summary>
    public const string DefaultAlias = "starlight";

    public static string DefaultAudience => $"{ResourceUri}/{DefaultAlias}/mcp";

    private readonly WebApplication _stubAs;

    public string Issuer { get; }

    /// <summary>The stub's own mutable state, for tests that drive its <c>/userinfo</c> behavior.
    /// Resolved out of the stub's DI container, so it is the same singleton its endpoints see.</summary>
    public StubAuthorizationServerState State { get; }

    public McpAuthTestConfigBuilder()
    {
        _stubAs = StubAuthorizationServerApp.Build([], builder => builder.WebHost.UseUrls("http://127.0.0.1:0"));
        _stubAs.Start();
        Issuer = _stubAs.Urls.First().TrimEnd('/');
        State = _stubAs.Services.GetRequiredService<StubAuthorizationServerState>();
        State.DefaultAudience = DefaultAudience;
    }

    /// <summary>Mints a token signed with this stub AS's own key/kid — the only combination its
    /// JWKS endpoint publishes, so this is the one signing key a token can use and still validate.
    /// <paramref name="audience"/> overrides the default per-alias resource, for testing what
    /// <c>McpAuth:ValidateAudience=false</c> actually stops checking.</summary>
    public string CreateAccessToken(
        IEnumerable<string> scopes,
        string? email = null,
        string subject = "test-subject",
        string? clientId = null,
        string? audience = null) =>
        TestTokenFactory.CreateAccessToken(
            SigningKey.Rsa,
            SigningKey.KeyId,
            Issuer,
            audience ?? DefaultAudience,
            scopes,
            subject,
            email: email,
            clientId: clientId);

    public PolarionMcpServerFactory Apply(PolarionMcpServerFactory factory) => factory
        .WithEnvironment(Environments.Development)
        .With("McpAuth:Enabled", "true")
        .With("McpAuth:Issuer", Issuer)
        .With("McpAuth:ResourceUri", ResourceUri);

    /// <summary>The OIDC scopes an Okta org authorization server can actually grant, and therefore
    /// the exact set such a deployment advertises. Notably absent: <c>polarion:read</c>, which the
    /// org authorization server has no way to issue.</summary>
    public static readonly string[] OrgAuthorizationServerScopes =
        ["openid", "email", "profile", "offline_access"];

    /// <summary>The org-authorization-server shape: no audience binding, a `cid` allowlist standing
    /// in for it, no scope requirement, and metadata advertising only the OIDC scopes the tenant can
    /// grant.</summary>
    public PolarionMcpServerFactory ApplyOrgAuthorizationServerShape(
        PolarionMcpServerFactory factory,
        params string[] allowedClientIds)
    {
        Apply(factory)
            .With("McpAuth:ValidateAudience", "false")
            .With("McpAuth:RequireScope", "false");

        for (var i = 0; i < OrgAuthorizationServerScopes.Length; i++)
            factory.With($"McpAuth:ScopesSupported:{i}", OrgAuthorizationServerScopes[i]);

        for (var i = 0; i < allowedClientIds.Length; i++)
            factory.With($"McpAuth:AllowedClientIds:{i}", allowedClientIds[i]);

        return factory;
    }

    public void Dispose()
    {
        _stubAs.StopAsync().GetAwaiter().GetResult();
        _stubAs.DisposeAsync().GetAwaiter().GetResult();
    }
}
