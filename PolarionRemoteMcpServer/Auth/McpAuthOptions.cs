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

    /// <summary>This server's own resource identifier — both the JWT audience and the RFC 9728
    /// protected-resource-metadata `resource` value, e.g.
    /// https://polarion-mcp.example.invalid/{alias}/mcp.</summary>
    public string ResourceUri { get; set; } = string.Empty;

    public List<string> ScopesSupported { get; set; } = [ApiScopes.PolarionRead];

    public int ClockSkewSeconds { get; set; } = 30;
}
