using PolarionRemoteMcpServer.Authentication;

namespace PolarionRemoteMcpServer.Auth;

// Deliberately `sealed class`, never `record` — a record's generated ToString() prints every
// property, so one `Log.Debug("{@Options}", o)` would dump config values that shouldn't be logged.
// This is the cheapest structural defense for the "no secret ever logged" rule.

public sealed class McpAuthOptions
{
    public const string SectionName = "McpAuth";

    public bool Enabled { get; set; }

    /// <summary>The external authorization server's issuer URI, e.g.
    /// https://issuer.example.invalid/oauth2/&lt;asid&gt; for an AS whose metadata lives under a
    /// path prefix, or a bare host for a self-hosted AS. JwtBearer fetches its OIDC discovery
    /// document and JWKS from this Authority.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Explicit override for the discovery document location. Optional escape hatch —
    /// a path-prefixed issuer answers on .../oauth2/&lt;asid&gt;/.well-known/openid-configuration
    /// (what .NET's Authority handling appends by default), but its
    /// .../oauth2/&lt;asid&gt;/.well-known/oauth-authorization-server is not in the MCP spec's
    /// client probe list, so it should not be relied on implicitly.</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>This server's own resource identifier, as a *base* URL with no alias and no
    /// trailing /mcp, e.g. https://polarion-mcp.example.invalid. Use <see cref="ResourceFor"/> to
    /// derive the per-alias RFC 9728 `resource` value and JWT audience —
    /// https://polarion-mcp.example.invalid/{alias}/mcp — since a single MCP server here serves
    /// several project aliases, each of which must advertise its own resource identifier.</summary>
    public string ResourceUri { get; set; } = string.Empty;

    /// <summary>Derives the per-alias resource identifier from <see cref="ResourceUri"/>.</summary>
    public string ResourceFor(string alias) => $"{ResourceUri.TrimEnd('/')}/{alias}/mcp";

    /// <summary>Replace-not-append: see <see cref="ReplaceConfiguredScopesSupported"/>. Binding
    /// McpAuth:ScopesSupported through ConfigurationBinder only ever appends to this default.</summary>
    public List<string> ScopesSupported { get; set; } = [ApiScopes.PolarionRead];

    public int ClockSkewSeconds { get; set; } = 30;

    /// <summary>When false, TokenValidationParameters stops binding `aud` to a per-alias resource
    /// URI. Opt-in escape hatch for an authorization server that cannot mint a per-resource
    /// audience, e.g. an Okta *org* authorization server whose `aud` is always its own issuer.
    /// <see cref="McpAuthOptionsValidator"/> refuses to start the app when this is false and
    /// <see cref="AllowedClientIds"/> is empty — that pairing would accept any token the
    /// authorization server issued to any client.</summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>Allowlist matched against the access token's `cid` claim — the substitute for
    /// audience binding when <see cref="ValidateAudience"/> is false. Empty means "no client
    /// check", which is refused by <see cref="McpAuthOptionsValidator"/> whenever ValidateAudience
    /// is also false.</summary>
    public List<string> AllowedClientIds { get; set; } = [];

    /// <summary>When false, the MCP read policy no longer asserts a scope claim. Opt-in for an
    /// authorization server with no custom-scope capability, where a scope value could not have
    /// conveyed resource-specific authorization anyway.</summary>
    public bool RequireScope { get; set; } = true;

    /// <summary>When false, the published RFC 9728 protected-resource metadata advertises no
    /// scopes at all, regardless of <see cref="ScopesSupported"/>. Needed because advertising a
    /// scope the authorization server cannot grant (e.g. polarion:read against an org AS) fails
    /// the whole authorization request with invalid_scope, and ConfigurationBinder's append-only
    /// semantics on ScopesSupported cannot otherwise express "advertise nothing".</summary>
    public bool AdvertiseScopes { get; set; } = true;
}
