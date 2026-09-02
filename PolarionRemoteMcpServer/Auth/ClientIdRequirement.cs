using Microsoft.AspNetCore.Authorization;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// Asserts the access token's `cid` claim names a client on <see cref="McpAuthOptions.AllowedClientIds"/>.
/// The documented substitute for the MCP spec's audience binding, for an authorization server that
/// cannot mint a per-resource `aud` — see <see cref="McpAuthOptions.ValidateAudience"/>.
/// </summary>
public sealed class ClientIdRequirement : IAuthorizationRequirement
{
    /// <summary>Okta's spelling on an access token. RFC 7662 introspection responses spell the same
    /// value `client_id`; this requirement only ever reads a validated JWT, never an introspection
    /// response, so one name is enough.</summary>
    public const string ClaimType = "cid";
}
