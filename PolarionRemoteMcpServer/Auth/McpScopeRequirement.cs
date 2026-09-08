using Microsoft.AspNetCore.Authorization;

namespace PolarionRemoteMcpServer.Auth;

/// <summary>
/// Replaces the MCP policy's former inline <c>RequireAssertion</c> scope check. The assertion form
/// had no way to reach <see cref="McpAuthOptions.RequireScope"/>: an AuthorizationHandlerContext
/// exposes no service provider, so config can only be consulted from a handler resolved out of DI.
///
/// Named <c>McpScope*</c>, not <c>Scope*</c>, because <see cref="Authentication.ScopeRequirement"/>
/// and <c>ScopeAuthorizationHandler</c> already exist for the REST API-key scheme.
/// </summary>
public sealed class McpScopeRequirement : IAuthorizationRequirement
{
    public McpScopeRequirement(string requiredScope)
    {
        RequiredScope = requiredScope;
    }

    public string RequiredScope { get; }
}
